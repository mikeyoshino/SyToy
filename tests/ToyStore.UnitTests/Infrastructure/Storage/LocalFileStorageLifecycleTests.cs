using Microsoft.Extensions.Options;
using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageLifecycleTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "toystore-media-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task CommitRetryReadAndDeleteAreIdempotent()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);

        await storage.CommitAsync(staged, CancellationToken.None);
        await storage.CommitAsync(staged, CancellationToken.None);
        await using var read = await storage.OpenReadAsync(
            staged.Media[0].StorageKey,
            CancellationToken.None);

        Assert.NotNull(read);
        Assert.Equal("image/jpeg", read.ContentType);
        Assert.Equal(LocalFileStorageStageTests.Jpeg().Length, read.Length);
        Assert.StartsWith("\"", read.EntityTag, StringComparison.Ordinal);
        Assert.Equal(LocalFileStorageStageTests.Jpeg(), await ReadAllAsync(read.Content));

        await storage.DeleteCommittedAsync(
            [staged.Media[0].StorageKey],
            CancellationToken.None);
        await storage.DeleteCommittedAsync(
            [staged.Media[0].StorageKey],
            CancellationToken.None);
        Assert.Null(await storage.OpenReadAsync(staged.Media[0].StorageKey, CancellationToken.None));
        Assert.False(Directory.Exists(Path.Combine(root, "files", staged.BatchToken)));
    }

    [Fact]
    public async Task CommitRetryFailsClosedWhenCommittedStructureWasChanged()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        await storage.CommitAsync(staged, CancellationToken.None);
        var file = Path.Combine(root, "files", staged.Media[0].StorageKey.Replace('/', Path.DirectorySeparatorChar));
        await File.AppendAllTextAsync(file, "tamper", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() =>
            storage.CommitAsync(staged, CancellationToken.None));
    }

    [Fact]
    public async Task CleanupOnlyRemovesOldStagingAndNeverCommittedMedia()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var old = await StageJpeg(storage);
        var recent = await StageJpeg(storage);
        var committed = await StageJpeg(storage);
        await storage.CommitAsync(committed, CancellationToken.None);
        Directory.SetLastWriteTimeUtc(
            Path.Combine(root, ".staging", old.BatchToken),
            DateTime.UtcNow.AddDays(-2));

        await storage.CleanupStagingAsync(
            DateTimeOffset.UtcNow.AddHours(-24),
            CancellationToken.None);

        Assert.False(Directory.Exists(Path.Combine(root, ".staging", old.BatchToken)));
        Assert.True(Directory.Exists(Path.Combine(root, ".staging", recent.BatchToken)));
        Assert.NotNull(await storage.OpenReadAsync(
            committed.Media[0].StorageKey,
            CancellationToken.None));
    }

    [Fact]
    public async Task DiscardIsIdempotentAndInvalidTokensDoNothing()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);

        await storage.DiscardStagingAsync("../files", CancellationToken.None);
        await storage.DiscardStagingAsync(staged.BatchToken, CancellationToken.None);
        await storage.DiscardStagingAsync(staged.BatchToken, CancellationToken.None);

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Fact]
    public async Task DestinationCollisionLeavesStagingAndExistingFinalUntouched()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        var final = Path.Combine(root, "files", staged.BatchToken);
        Directory.CreateDirectory(final);
        await File.WriteAllTextAsync(
            Path.Combine(final, "sentinel"),
            "keep",
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() => storage.CommitAsync(staged, CancellationToken.None));

        Assert.True(Directory.Exists(Path.Combine(root, ".staging", staged.BatchToken)));
        Assert.Equal("keep", await File.ReadAllTextAsync(
            Path.Combine(final, "sentinel"),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ForgedDeleteBatchDoesNotDeleteAnyValidCommittedFile()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        await storage.CommitAsync(staged, CancellationToken.None);

        await storage.DeleteCommittedAsync(
            [staged.Media[0].StorageKey, "../outside.jpg"],
            CancellationToken.None);

        Assert.NotNull(await storage.OpenReadAsync(staged.Media[0].StorageKey, CancellationToken.None));
    }

    [Fact]
    public async Task DescriptorOverFiveMebibytesIsRejectedWithoutMovingStaging()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        var item = staged.Media[0] with { Length = BoundedImageWriter.MaximumBytes + 1L };
        var forged = new StagedMediaBatch(staged.BatchToken, [item]);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            storage.CommitAsync(forged, CancellationToken.None));

        Assert.True(Directory.Exists(Path.Combine(root, ".staging", staged.BatchToken)));
        Assert.False(Directory.Exists(Path.Combine(root, "files", staged.BatchToken)));
    }

    [Fact]
    public async Task NewStorageChildrenUseRestrictiveUnixModes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        var stagedFile = Path.Combine(root, ".staging", staged.Media[0].StorageKey.Replace('/', Path.DirectorySeparatorChar));

        Assert.Equal((UnixFileMode)0x1e8, File.GetUnixFileMode(Path.Combine(root, ".staging")));
        Assert.Equal((UnixFileMode)0x1e8, File.GetUnixFileMode(Path.Combine(root, "files")));
        Assert.Equal((UnixFileMode)0x1e8, File.GetUnixFileMode(Path.GetDirectoryName(stagedFile)!));
        Assert.Equal((UnixFileMode)0x1a0, File.GetUnixFileMode(stagedFile));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OpenReadReturnsNullWhenFileOrDirectoryDisappearsBeforeOpen(bool removeDirectory)
    {
        var removeBeforeOpen = false;
        using var storage = new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = root }),
            TimeProvider.System,
            new MediaIdGenerator(),
            filePath =>
            {
                if (!removeBeforeOpen)
                {
                    return;
                }

                if (removeDirectory)
                {
                    Directory.Delete(Path.GetDirectoryName(filePath)!, true);
                }
                else
                {
                    File.Delete(filePath);
                }
            });
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        await storage.CommitAsync(staged, CancellationToken.None);
        removeBeforeOpen = true;

        var read = await storage.OpenReadAsync(
            staged.Media[0].StorageKey,
            CancellationToken.None);

        Assert.Null(read);
    }

    [Fact]
    public async Task CommittedDeletePreflightsWholeBatchAndRefusesSymlinkBeforePartialDeletion()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var stage = await storage.StageAsync(
            [
                new MediaUpload(new MemoryStream(LocalFileStorageStageTests.Jpeg()), "image/jpeg"),
                new MediaUpload(new MemoryStream(LocalFileStorageStageTests.Png()), "image/png"),
            ],
            CancellationToken.None);
        await storage.CommitAsync(stage.Value, CancellationToken.None);
        var outside = Path.Combine(Path.GetTempPath(), "toystore-delete-sentinel", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        var batch = Path.Combine(root, "files", stage.Value.BatchToken);
        File.CreateSymbolicLink(Path.Combine(batch, "outside.jpg"), sentinel);

        await Assert.ThrowsAsync<IOException>(() => storage.DeleteCommittedAsync(
            [stage.Value.Media[0].StorageKey],
            CancellationToken.None));

        Assert.True(File.Exists(Path.Combine(
            root,
            "files",
            stage.Value.Media[0].StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        Assert.True(File.Exists(Path.Combine(
            root,
            "files",
            stage.Value.Media[1].StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel, TestContext.Current.CancellationToken));
        File.Delete(Path.Combine(batch, "outside.jpg"));
        Directory.Delete(outside, true);
    }

    [Fact]
    public async Task CommitRenamePermissionFailureLeavesStagingAndNoPartialFinal()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        var files = Path.Combine(root, "files");
        File.SetUnixFileMode(
            files,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            await Assert.ThrowsAnyAsync<Exception>(() =>
                storage.CommitAsync(staged, CancellationToken.None));

            Assert.True(Directory.Exists(Path.Combine(root, ".staging", staged.BatchToken)));
            Assert.False(Directory.Exists(Path.Combine(root, "files", staged.BatchToken)));
        }
        finally
        {
            File.SetUnixFileMode(
                files,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    [Fact]
    public async Task OpenReadRejectsCommittedDirectoryWithoutOwnershipMarker()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var staged = await StageJpeg(storage);
        await storage.CommitAsync(staged, CancellationToken.None);
        File.Delete(Path.Combine(root, "files", staged.BatchToken, ".owner"));

        var read = await storage.OpenReadAsync(
            staged.Media[0].StorageKey,
            CancellationToken.None);

        Assert.Null(read);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }
    }

    private LocalFileStorage CreateStorage() => new(
        Options.Create(new LocalFileStorageOptions { RootPath = root }),
        TimeProvider.System);

    private static async Task<StagedMediaBatch> StageJpeg(LocalFileStorage storage)
    {
        var result = await storage.StageAsync(
            [new MediaUpload(new MemoryStream(LocalFileStorageStageTests.Jpeg()), "image/jpeg")],
            CancellationToken.None);
        return result.Value;
    }

    private static async Task<byte[]> ReadAllAsync(Stream stream)
    {
        using var output = new MemoryStream();
        await stream.CopyToAsync(output);
        return output.ToArray();
    }
}
