using Microsoft.Extensions.Options;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Storage;

public sealed class LocalFileStorage : IFileStorage, IFileStorageInitializer, IDisposable
{
    private const string StagingDirectoryName = ".staging";
    private const string FilesDirectoryName = "files";
    private const string OwnershipMarkerName = ".owner";
    private const string ProbePrefix = ".probe-";
    private const string ProbeFileName = "probe.bin";
    private const UnixFileMode ApprovedDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
    private readonly string rootPath;
    private readonly string stagingPath;
    private readonly string filesPath;
    private readonly TimeSpan stagingRetention;
    private readonly TimeProvider timeProvider;
    private readonly IMediaIdGenerator idGenerator;
    private readonly Action<string>? beforeOpen;
    private readonly SemaphoreSlim initializationLock = new(1, 1);
    private volatile bool initialized;
    private bool disposed;

    public LocalFileStorage(IOptions<LocalFileStorageOptions> options, TimeProvider timeProvider)
        : this(options, timeProvider, new MediaIdGenerator())
    {
    }

    internal LocalFileStorage(
        IOptions<LocalFileStorageOptions> options,
        TimeProvider timeProvider,
        IMediaIdGenerator idGenerator,
        Action<string>? beforeOpen = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(idGenerator);

        if (!Path.IsPathFullyQualified(options.Value.RootPath))
        {
            throw new InvalidOperationException("Storage root must be an absolute path.");
        }

        var configuredRoot = Path.GetFullPath(options.Value.RootPath);
        if (StoragePathResolver.IsReparsePoint(configuredRoot))
        {
            throw new IOException("Storage root must not be a symbolic link or reparse point.");
        }

        rootPath = StoragePathResolver.ResolveExistingAliases(
            configuredRoot,
            includeLeaf: false);
        stagingPath = ContainedPath(rootPath, StagingDirectoryName);
        filesPath = ContainedPath(rootPath, FilesDirectoryName);
        stagingRetention = options.Value.StagingRetention;
        this.timeProvider = timeProvider;
        this.idGenerator = idGenerator;
        this.beforeOpen = beforeOpen;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (initialized)
        {
            return;
        }

        await initializationLock.WaitAsync(cancellationToken);
        try
        {
            if (initialized)
            {
                return;
            }

            EnsureStorageRootDirectory(rootPath);
            EnsureNotReparsePoint(rootPath);
            ValidateDirectoryMode(rootPath);

            // Preflight both operator-managed entries before creating either missing sibling.
            // An unsafe existing child must fail startup without mutating the storage tree.
            ValidateExistingFixedDirectory(stagingPath);
            ValidateExistingFixedDirectory(filesPath);
            EnsureFixedDirectory(stagingPath);
            EnsureFixedDirectory(filesPath);

            CleanupStartupProbeArtifacts();
            await ProbeAtomicCommitAsync(cancellationToken);
            await CleanupStagingCoreAsync(
                timeProvider.GetUtcNow().Subtract(stagingRetention),
                cancellationToken);
            initialized = true;
        }
        finally
        {
            initializationLock.Release();
        }
    }

    public async Task<Result<StagedMediaBatch>> StageAsync(
        IReadOnlyCollection<MediaUpload> uploads,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(uploads);
        var uploadSnapshot = uploads.ToArray();
        if (uploadSnapshot.Length == 0)
        {
            return Result<StagedMediaBatch>.Failure(MediaStorageErrors.EmptyBatch);
        }

        if (uploadSnapshot.Length > Product.MaximumImageCount)
        {
            return Result<StagedMediaBatch>.Failure(MediaStorageErrors.TooManyFiles);
        }

        string? batchPath = null;
        var media = new List<StagedMedia>(uploadSnapshot.Length);

        try
        {
            var batch = CreateExclusiveBatchDirectory();
            var batchId = batch.BatchId;
            batchPath = batch.Path;
            foreach (var upload in uploadSnapshot)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!ImageSignatureValidator.IsSupportedContentType(upload.ContentType))
                {
                    await CleanupFailedBatchAsync(batchPath, null);
                    return Result<StagedMediaBatch>.Failure(MediaStorageErrors.UnsupportedContentType);
                }

                var fileId = idGenerator.CreateId();
                if (!StorageKey.IsBatchId(fileId))
                {
                    throw new InvalidOperationException("The media identity generator returned an invalid identifier.");
                }

                var writingPath = ContainedPath(batchPath, $".{fileId}.writing");
                Result<BoundedImageWrite> writeResult;
                await using (var output = new FileStream(
                    writingPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan))
                {
                    SetFileMode(writingPath);
                    writeResult = await BoundedImageWriter.CopyAsync(
                        upload.Content,
                        output,
                        upload.ContentType,
                        cancellationToken);
                }

                if (writeResult.IsFailure)
                {
                    await CleanupFailedBatchAsync(batchPath, null);
                    return Result<StagedMediaBatch>.Failure(writeResult.Error);
                }

                var fileName = fileId + writeResult.Value.MediaType.Extension;
                var readyPath = ContainedPath(batchPath, fileName);
                File.Move(writingPath, readyPath);
                SetFileMode(readyPath);
                var key = $"{batchId}/{fileName}";

                string? thumbnailKey = null;
                long? thumbnailLength = null;
                if (upload.GenerateProductThumbnail)
                {
                    var thumbnailId = idGenerator.CreateId();
                    if (!StorageKey.IsBatchId(thumbnailId))
                    {
                        throw new InvalidOperationException("The media identity generator returned an invalid identifier.");
                    }

                    var thumbnailFileName = thumbnailId + ".webp";
                    var thumbnailPath = ContainedPath(batchPath, thumbnailFileName);
                    var thumbnailResult = ProductThumbnailGenerator.Create(readyPath, thumbnailPath);
                    if (thumbnailResult.IsFailure)
                    {
                        await CleanupFailedBatchAsync(batchPath, null);
                        return Result<StagedMediaBatch>.Failure(thumbnailResult.Error);
                    }

                    SetFileMode(thumbnailPath);
                    thumbnailKey = $"{batchId}/{thumbnailFileName}";
                    thumbnailLength = thumbnailResult.Value;
                }
                media.Add(new StagedMedia(
                    batchId,
                    key,
                    $"/media/{key}",
                    writeResult.Value.MediaType.ContentType,
                    writeResult.Value.Length,
                    thumbnailKey,
                    thumbnailKey is null ? null : $"/media/{thumbnailKey}",
                    thumbnailLength));
            }

            return Result<StagedMediaBatch>.Success(new StagedMediaBatch(batchId, media));
        }
        catch (Exception exception)
        {
            if (batchPath is not null)
            {
                await CleanupFailedBatchAsync(batchPath, exception);
            }

            throw;
        }
    }

    public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();
        ValidateBatch(batch);

        var source = ContainedPath(stagingPath, batch.BatchToken);
        var destination = ContainedPath(filesPath, batch.BatchToken);
        var sourceExists = Directory.Exists(source);
        var destinationExists = Directory.Exists(destination);

        if (sourceExists && destinationExists)
        {
            throw new IOException("Media staging and committed destinations both exist.");
        }

        if (destinationExists)
        {
            VerifyBatchDirectory(destination, batch);
            return Task.CompletedTask;
        }

        if (!sourceExists)
        {
            throw new DirectoryNotFoundException("The staged media batch does not exist.");
        }

        EnsureNotReparsePoint(source);
        VerifyBatchDirectory(source, batch);
        Directory.Move(source, destination);
        EnsureNotReparsePoint(destination);
        return Task.CompletedTask;
    }

    public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken)
    {
        EnsureInitialized();
        cancellationToken.ThrowIfCancellationRequested();
        if (!StorageKey.IsBatchId(batchToken))
        {
            return Task.CompletedTask;
        }

        var path = ContainedPath(stagingPath, batchToken);
        DeleteDirectoryIfPresent(path);
        return Task.CompletedTask;
    }

    public Task DeleteCommittedAsync(
        IReadOnlyCollection<string> storageKeys,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        ArgumentNullException.ThrowIfNull(storageKeys);
        cancellationToken.ThrowIfCancellationRequested();
        var parsed = new List<StorageKey>(storageKeys.Count);
        foreach (var value in storageKeys)
        {
            if (!StorageKey.TryParse(value, out var key))
            {
                return Task.CompletedTask;
            }

            parsed.Add(key);
        }

        foreach (var group in parsed.GroupBy(key => key.BatchId, StringComparer.Ordinal))
        {
            var batchPath = ContainedPath(filesPath, group.Key);
            if (!Directory.Exists(batchPath))
            {
                continue;
            }

            EnsureTreeHasNoReparsePoints(batchPath);
            var ownershipMarker = ContainedPath(batchPath, OwnershipMarkerName);
            if (!File.Exists(ownershipMarker))
            {
                throw new IOException("The committed media batch ownership marker is missing.");
            }

            foreach (var key in group)
            {
                var filePath = ContainedPath(batchPath, key.FileName);
                if (File.Exists(filePath))
                {
                    EnsureNotReparsePoint(filePath);
                    File.Delete(filePath);
                }
            }

            var remaining = Directory.EnumerateFileSystemEntries(batchPath)
                .Where(path => !string.Equals(
                    Path.GetFileName(path),
                    OwnershipMarkerName,
                    StringComparison.Ordinal))
                .ToArray();
            if (remaining.Length == 0)
            {
                File.Delete(ownershipMarker);
                Directory.Delete(batchPath);
            }
        }

        return Task.CompletedTask;
    }

    public Task<StoredMediaRead?> OpenReadAsync(
        string storageKey,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        cancellationToken.ThrowIfCancellationRequested();
        if (!StorageKey.TryParse(storageKey, out var key))
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }

        var batchPath = ContainedPath(filesPath, key.BatchId);
        var filePath = ContainedPath(batchPath, key.FileName);
        var ownershipMarker = ContainedPath(batchPath, OwnershipMarkerName);
        if (!Directory.Exists(batchPath) || !File.Exists(filePath))
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }

        try
        {
            if (!File.Exists(ownershipMarker) ||
                IsReparsePoint(batchPath) ||
                IsReparsePoint(ownershipMarker) ||
                IsReparsePoint(filePath))
            {
                return Task.FromResult<StoredMediaRead?>(null);
            }
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }
        FileStream? stream = null;
        try
        {
            beforeOpen?.Invoke(filePath);
            stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
            var header = new byte[16];
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            stream.Position = 0;
            var declared = ContentTypeForExtension(key.Extension);
            var validation = ImageSignatureValidator.Validate(header.AsSpan(0, read), declared);
            if (validation.IsFailure)
            {
                stream.Dispose();
                return Task.FromResult<StoredMediaRead?>(null);
            }

            var lastModified = new DateTimeOffset(File.GetLastWriteTimeUtc(filePath), TimeSpan.Zero);
            var entityTag = $"\"{stream.Length:x}-{lastModified.UtcTicks:x}\"";
            StoredMediaRead result = new(
                stream,
                validation.Value.ContentType,
                stream.Length,
                lastModified,
                entityTag);
            stream = null;
            return Task.FromResult<StoredMediaRead?>(result);
        }
        catch (FileNotFoundException)
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }
        catch (DirectoryNotFoundException)
        {
            return Task.FromResult<StoredMediaRead?>(null);
        }
        finally
        {
            stream?.Dispose();
        }
    }

    public Task CleanupStagingAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken)
    {
        EnsureInitialized();
        return CleanupStagingCoreAsync(olderThanUtc, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        initializationLock.Dispose();
        disposed = true;
    }

    private Task CleanupStagingCoreAsync(
        DateTimeOffset olderThanUtc,
        CancellationToken cancellationToken)
    {
        if (olderThanUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("The staging cutoff must be UTC.", nameof(olderThanUtc));
        }

        foreach (var path in Directory.EnumerateFileSystemEntries(stagingPath))
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (File.Exists(path) || !StorageKey.IsBatchId(Path.GetFileName(path)))
            {
                throw new IOException("The staging directory contains an unexpected entry.");
            }

            EnsureNotReparsePoint(path);
            var lastWrite = new DateTimeOffset(Directory.GetLastWriteTimeUtc(path), TimeSpan.Zero);
            if (lastWrite < olderThanUtc)
            {
                DeleteDirectoryIfPresent(path);
            }
        }

        return Task.CompletedTask;
    }

    private async Task ProbeAtomicCommitAsync(CancellationToken cancellationToken)
    {
        var probeId = idGenerator.CreateId();
        if (!StorageKey.IsBatchId(probeId))
        {
            throw new InvalidOperationException(
                "The media identity generator returned an invalid identifier.");
        }

        var probeName = ProbePrefix + probeId;
        var source = ContainedPath(stagingPath, probeName);
        var destination = ContainedPath(filesPath, probeName);
        try
        {
            if (Path.Exists(source) || Path.Exists(destination))
            {
                if (Path.Exists(source))
                {
                    EnsureNotReparsePoint(source);
                }

                if (Path.Exists(destination))
                {
                    EnsureNotReparsePoint(destination);
                }

                throw new IOException("The startup media probe destination is occupied.");
            }

            Directory.CreateDirectory(source);
            EnsureNotReparsePoint(source);
            SetDirectoryMode(source);
            var probeFile = ContainedPath(source, ProbeFileName);
            await using (var stream = new FileStream(
                probeFile,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                1,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                SetFileMode(probeFile);
                await stream.WriteAsync(new byte[] { 0x01 }, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            Directory.Move(source, destination);
            EnsureTreeHasNoReparsePoints(destination);
            DeleteDirectoryIfPresent(destination);
        }
        catch (Exception exception)
        {
            var errors = new List<Exception> { exception };
            TryCleanupProbe(source, errors);
            TryCleanupProbe(destination, errors);
            if (errors.Count > 1)
            {
                throw new AggregateException(errors);
            }

            throw;
        }
    }

    private void CleanupStartupProbeArtifacts()
    {
        CleanupProbeArtifactsIn(stagingPath);
        CleanupProbeArtifactsIn(filesPath);
    }

    private static void CleanupProbeArtifactsIn(string parent)
    {
        foreach (var path in Directory.EnumerateFileSystemEntries(parent))
        {
            var name = Path.GetFileName(path);
            if (!name.StartsWith(ProbePrefix, StringComparison.Ordinal) ||
                !StorageKey.IsBatchId(name[ProbePrefix.Length..]))
            {
                continue;
            }

            if (!Directory.Exists(path))
            {
                throw new IOException("A startup media probe artifact is not a directory.");
            }

            DeleteDirectoryIfPresent(path);
        }
    }

    private static void TryCleanupProbe(string path, List<Exception> errors)
    {
        try
        {
            DeleteDirectoryIfPresent(path);
        }
        catch (Exception cleanupException)
        {
            errors.Add(cleanupException);
        }
    }

    private static void ValidateBatch(StagedMediaBatch batch)
    {
        if (!StorageKey.IsBatchId(batch.BatchToken) ||
            batch.Media.Count is < 1 or > Product.MaximumImageCount)
        {
            throw new InvalidOperationException("The staged media batch descriptor is invalid.");
        }

        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var media in batch.Media)
        {
            if (!StorageKey.TryParse(media.StorageKey, out var key) ||
                !string.Equals(key.BatchId, batch.BatchToken, StringComparison.Ordinal) ||
                !string.Equals(media.BatchToken, batch.BatchToken, StringComparison.Ordinal) ||
                !string.Equals(media.PublicRelativeUrl, $"/media/{media.StorageKey}", StringComparison.Ordinal) ||
                !string.Equals(media.ContentType, ContentTypeForExtension(key.Extension), StringComparison.Ordinal) ||
                media.Length is <= 0 or > BoundedImageWriter.MaximumBytes ||
                !keys.Add(media.StorageKey)
                || !TryValidateOptionalThumbnailDescriptor(media, batch.BatchToken, keys))
            {
                throw new InvalidOperationException("The staged media batch descriptor is invalid.");
            }
        }
    }

    private static void VerifyBatchDirectory(string directory, StagedMediaBatch batch)
    {
        EnsureNotReparsePoint(directory);
        var expected = batch.Media
            .SelectMany(media => media.ThumbnailStorageKey is null
                ? [new StagedFileDescriptor(media.StorageKey, media.Length, media.ContentType)]
                : new[]
                {
                    new StagedFileDescriptor(media.StorageKey, media.Length, media.ContentType),
                    new StagedFileDescriptor(media.ThumbnailStorageKey, media.ThumbnailLength!.Value, "image/webp"),
                })
            .ToDictionary(
                media => StorageKey.TryParse(media.StorageKey, out var key)
                    ? key.FileName
                    : throw new InvalidOperationException("The staged media key is invalid."),
                StringComparer.Ordinal);
        var entries = Directory.EnumerateFileSystemEntries(directory).ToArray();
        if (entries.Length != expected.Count + 1)
        {
            throw new IOException("The media batch structure does not match its descriptor.");
        }

        var ownershipMarker = ContainedPath(directory, OwnershipMarkerName);
        if (!File.Exists(ownershipMarker) || IsReparsePoint(ownershipMarker) ||
            new FileInfo(ownershipMarker).Length != 0)
        {
            throw new IOException("The media batch ownership marker is missing or invalid.");
        }

        var header = new byte[16];
        foreach (var entry in entries.Where(entry => !string.Equals(
                     Path.GetFileName(entry),
                     OwnershipMarkerName,
                     StringComparison.Ordinal)))
        {
            var fileName = Path.GetFileName(entry);
            if (!expected.TryGetValue(fileName, out var descriptor) || !File.Exists(entry))
            {
                throw new IOException("The media batch structure does not match its descriptor.");
            }

            EnsureNotReparsePoint(entry);
            var info = new FileInfo(entry);
            if (info.Length != descriptor.Length ||
                !StorageKey.TryParse(descriptor.StorageKey, out var parsed))
            {
                throw new IOException("The media batch structure does not match its descriptor.");
            }

            using var stream = File.OpenRead(entry);
            var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
            var validation = ImageSignatureValidator.Validate(
                header.AsSpan(0, read),
                ContentTypeForExtension(parsed.Extension));
            if (validation.IsFailure)
            {
                throw new IOException("The media batch contains an invalid file.");
            }
        }
    }

    private static bool TryValidateOptionalThumbnailDescriptor(
        StagedMedia media,
        string batchToken,
        HashSet<string> keys)
    {
        if (media.ThumbnailStorageKey is null
            && media.ThumbnailPublicRelativeUrl is null
            && media.ThumbnailLength is null)
        {
            return true;
        }

        if (!StorageKey.TryParse(media.ThumbnailStorageKey, out var thumbnail)
            || thumbnail.Extension != "webp"
            || thumbnail.BatchId != batchToken
            || media.ThumbnailPublicRelativeUrl != $"/media/{media.ThumbnailStorageKey}"
            || media.ThumbnailLength is null or <= 0
            || !keys.Add(media.ThumbnailStorageKey!))
        {
            return false;
        }

        return true;
    }

    private sealed record StagedFileDescriptor(string StorageKey, long Length, string ContentType);

    private static string ContentTypeForExtension(string extension) => extension switch
    {
        "jpg" => "image/jpeg",
        "png" => "image/png",
        "webp" => "image/webp",
        _ => throw new InvalidOperationException("The media extension is invalid."),
    };

    private static async Task CleanupFailedBatchAsync(string batchPath, Exception? original)
    {
        try
        {
            await Task.Run(() => DeleteDirectoryIfPresent(batchPath), CancellationToken.None);
        }
        catch (Exception cleanupException) when (original is not null)
        {
            throw new AggregateException(original, cleanupException);
        }
    }

    private static void DeleteDirectoryIfPresent(string path)
    {
        if (!Directory.Exists(path))
        {
            return;
        }

        EnsureTreeHasNoReparsePoints(path);
        Directory.Delete(path, recursive: true);
    }

    private ExclusiveBatch CreateExclusiveBatchDirectory()
    {
        const int maximumAttempts = 32;
        for (var attempt = 0; attempt < maximumAttempts; attempt++)
        {
            var batchId = idGenerator.CreateId();
            if (!StorageKey.IsBatchId(batchId))
            {
                throw new InvalidOperationException(
                    "The media identity generator returned an invalid identifier.");
            }

            var candidate = ContainedPath(stagingPath, batchId);
            var committedCandidate = ContainedPath(filesPath, batchId);
            if (StoragePathResolver.IsReparsePoint(committedCandidate))
            {
                throw new IOException(
                    "Symbolic links and reparse points are not allowed in media storage.");
            }

            if (Path.Exists(committedCandidate))
            {
                continue;
            }

            if (StoragePathResolver.IsReparsePoint(candidate))
            {
                throw new IOException(
                    "Symbolic links and reparse points are not allowed in media storage.");
            }

            if (Path.Exists(candidate))
            {
                continue;
            }

            Directory.CreateDirectory(candidate);
            EnsureNotReparsePoint(candidate);
            var marker = ContainedPath(candidate, OwnershipMarkerName);
            var ownsMarker = false;
            try
            {
                using (new FileStream(
                    marker,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None))
                {
                }

                ownsMarker = true;
                SetFileMode(marker);
                SetDirectoryMode(candidate);
                if (StoragePathResolver.IsReparsePoint(committedCandidate))
                {
                    throw new IOException(
                        "Symbolic links and reparse points are not allowed in media storage.");
                }

                if (Path.Exists(committedCandidate))
                {
                    DeleteDirectoryIfPresent(candidate);
                    continue;
                }

                return new ExclusiveBatch(batchId, candidate);
            }
            catch (IOException) when (!ownsMarker && File.Exists(marker))
            {
                // Another staging operation won the same generated identity. Its marker and
                // directory are not ours to delete; a fresh 128-bit identity is allocated.
            }
            catch
            {
                if (ownsMarker)
                {
                    DeleteDirectoryIfPresent(candidate);
                }

                throw;
            }
        }

        throw new IOException("Unable to allocate a unique media staging batch.");
    }

    private static void EnsureStorageRootDirectory(string path)
    {
        if (Path.Exists(path))
        {
            EnsureNotReparsePoint(path);
        }

        if (File.Exists(path) && !Directory.Exists(path))
        {
            throw new IOException("A required storage directory is occupied by a file.");
        }

        if (Directory.Exists(path))
        {
            return;
        }

        if (OperatingSystem.IsWindows())
        {
            Directory.CreateDirectory(path);
        }
        else
        {
            // Supply the restrictive mode to the create syscall. The configured root is
            // never chmod'd: an existing operator-managed root is validation-only.
            Directory.CreateDirectory(path, ApprovedDirectoryMode);
        }

        EnsureNotReparsePoint(path);
    }

    private static void ValidateExistingFixedDirectory(string path)
    {
        if (!Path.Exists(path))
        {
            return;
        }

        EnsureNotReparsePoint(path);
        if (!Directory.Exists(path))
        {
            throw new IOException("A required storage directory is occupied by a file.");
        }

        ValidateDirectoryMode(path);
    }

    private static void EnsureFixedDirectory(string path)
    {
        if (!Directory.Exists(path))
        {
            if (OperatingSystem.IsWindows())
            {
                Directory.CreateDirectory(path);
            }
            else
            {
                Directory.CreateDirectory(path, ApprovedDirectoryMode);
            }
        }

        EnsureNotReparsePoint(path);
        ValidateDirectoryMode(path);
    }

    private static void ValidateDirectoryMode(string path)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var actualMode = File.GetUnixFileMode(path);
        if ((actualMode & ~ApprovedDirectoryMode) != 0)
        {
            throw new IOException(
                $"Storage directory permissions must not exceed 0750: '{path}'.");
        }
    }

    private static void EnsureNotReparsePoint(string path)
    {
        if (IsReparsePoint(path))
        {
            throw new IOException("Symbolic links and reparse points are not allowed in media storage.");
        }
    }

    private static bool IsReparsePoint(string path) =>
        (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;

    private static void EnsureTreeHasNoReparsePoints(string path)
    {
        EnsureNotReparsePoint(path);
        foreach (var child in Directory.EnumerateFileSystemEntries(path))
        {
            EnsureNotReparsePoint(child);
            if (Directory.Exists(child))
            {
                EnsureTreeHasNoReparsePoints(child);
            }
        }
    }

    private static string ContainedPath(string parent, string child)
    {
        if (child.Contains('\0', StringComparison.Ordinal))
        {
            throw new InvalidOperationException("The storage path is invalid.");
        }

        var result = Path.GetFullPath(Path.Combine(parent, child));
        var prefix = Path.TrimEndingDirectorySeparator(Path.GetFullPath(parent)) + Path.DirectorySeparatorChar;
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        if (!result.StartsWith(prefix, comparison))
        {
            throw new InvalidOperationException("The storage path escapes its configured root.");
        }

        return result;
    }

    private static void SetDirectoryMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                ApprovedDirectoryMode);
        }
    }

    private static void SetFileMode(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            File.SetUnixFileMode(
                path,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead);
        }
    }

    private void EnsureInitialized()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!initialized)
        {
            throw new InvalidOperationException("File storage has not been initialized.");
        }
    }

    private sealed record ExclusiveBatch(string BatchId, string Path);
}
