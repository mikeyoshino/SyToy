using Microsoft.Extensions.Options;
using SkiaSharp;
using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageStageTests : IDisposable
{
    private readonly string root = Path.Combine(Path.GetTempPath(), "toystore-media-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StagePreservesInputOrderAndUsesGeneratedNames()
    {
        var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var uploads = new[]
        {
            new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg", true),
            new MediaUpload(new MemoryStream(Png()), "image/png", true),
        };

        var result = await storage.StageAsync(uploads, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(["image/jpeg", "image/png"], result.Value.Media.Select(media => media.ContentType));
        Assert.All(result.Value.Media, media => Assert.Equal(result.Value.BatchToken, media.BatchToken));
        Assert.EndsWith(".jpg", result.Value.Media[0].StorageKey, StringComparison.Ordinal);
        Assert.EndsWith(".png", result.Value.Media[1].StorageKey, StringComparison.Ordinal);
        Assert.All(result.Value.Media, media => Assert.StartsWith("/media/", media.PublicRelativeUrl, StringComparison.Ordinal));
        Assert.All(result.Value.Media, media =>
        {
            Assert.EndsWith(".webp", media.ThumbnailStorageKey, StringComparison.Ordinal);
            Assert.StartsWith("/media/", media.ThumbnailPublicRelativeUrl, StringComparison.Ordinal);
            Assert.True(media.ThumbnailLength > 0);
            Assert.NotEqual(media.StorageKey, media.ThumbnailStorageKey);
        });
    }

    [Fact]
    public async Task InvalidSecondItemRemovesTheWholeBatch()
    {
        var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var result = await storage.StageAsync(
            [
                new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg"),
                new MediaUpload(new MemoryStream([1, 2, 3]), "image/png"),
            ],
            CancellationToken.None);

        Assert.Equal(MediaStorageErrors.InvalidSignature, result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Fact]
    public async Task EmptyAndOverLimitUseStableResultFailures()
    {
        var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);

        var empty = await storage.StageAsync([], CancellationToken.None);
        var tooMany = await storage.StageAsync(
            Enumerable.Range(0, 9)
                .Select(_ => new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg"))
                .ToArray(),
            CancellationToken.None);

        Assert.Equal(MediaStorageErrors.EmptyBatch, empty.Error);
        Assert.Equal(MediaStorageErrors.TooManyFiles, tooMany.Error);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("image/jpeg; charset=binary")]
    public async Task InvalidDeclaredMimeUsesStableResultAndLeavesNoStaging(string contentType)
    {
        var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);

        var result = await storage.StageAsync(
            [new MediaUpload(new MemoryStream(Jpeg()), contentType)],
            CancellationToken.None);

        Assert.Equal(MediaStorageErrors.UnsupportedContentType, result.Error);
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public async Task StagePreservesEveryPositionForOneThroughEightFiles(int count)
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var uploads = Enumerable.Range(0, count)
            .Select(index => new MediaUpload(
                new MemoryStream(index % 2 == 0 ? Jpeg() : Png()),
                index % 2 == 0 ? "image/jpeg" : "image/png"))
            .ToArray();

        var result = await storage.StageAsync(uploads, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(count, result.Value.Media.Count);
        Assert.Equal(
            Enumerable.Range(0, count).Select(index => index % 2 == 0 ? "image/jpeg" : "image/png"),
            result.Value.Media.Select(media => media.ContentType));
    }

    [Fact]
    public async Task ConcurrentStagingProducesDistinctBatchesAndKeys()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);

        var results = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => storage.StageAsync(
            [new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg")],
            CancellationToken.None)));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(16, results.Select(result => result.Value.BatchToken).Distinct().Count());
        Assert.Equal(16, results.Select(result => result.Value.Media[0].StorageKey).Distinct().Count());
    }

    [Fact]
    public async Task DeterministicConcurrentBatchCollisionIsExclusiveAndBothOperationsRecover()
    {
        using var generator = new BarrierCollisionGenerator();
        using var storage = new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = root }),
            TimeProvider.System,
            generator);
        await storage.InitializeAsync(CancellationToken.None);

        var first = Task.Run(() => storage.StageAsync(
            [new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg")],
            CancellationToken.None), TestContext.Current.CancellationToken);
        var second = Task.Run(() => storage.StageAsync(
            [new MediaUpload(new MemoryStream(Png()), "image/png")],
            CancellationToken.None), TestContext.Current.CancellationToken);
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, results.Select(result => result.Value.BatchToken).Distinct().Count());
        Assert.All(results, result => Assert.True(File.Exists(Path.Combine(
            root,
            ".staging",
            result.Value.BatchToken,
            ".owner"))));
        await Task.WhenAll(results.Select(result =>
            storage.CommitAsync(result.Value, CancellationToken.None)));
        Assert.All(results, result => Assert.True(File.Exists(Path.Combine(
            root,
            "files",
            result.Value.BatchToken,
            ".owner"))));
    }

    [Fact]
    public async Task AllocatorSkipsIdentityAlreadyPresentInCommittedTree()
    {
        const string collision = "aabbccddeeff00112233445566778899";
        const string alternative = "11223344556677889900aabbccddeeff";
        const string fileId = "00112233445566778899aabbccddeeff";
        using var storage = new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = root }),
            TimeProvider.System,
            new SequenceGenerator(
                new MediaIdGenerator().CreateId(),
                collision,
                alternative,
                fileId));
        await storage.InitializeAsync(CancellationToken.None);
        Directory.CreateDirectory(Path.Combine(root, "files", collision));

        var result = await storage.StageAsync(
            [new MediaUpload(new MemoryStream(Jpeg()), "image/jpeg")],
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(alternative, result.Value.BatchToken);
    }

    [Fact]
    public async Task MidStreamCancellationLeavesNoBatchOrWritingResidue()
    {
        using var cancellation = new CancellationTokenSource();
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        await using var input = new CancelAfterFirstReadStream(Jpeg(), cancellation);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => storage.StageAsync(
            [new MediaUpload(input, "image/jpeg")],
            cancellation.Token));

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Fact]
    public async Task UnexpectedInputIoFailureLeavesNoBatchOrWritingResidue()
    {
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        await using var input = new ThrowAfterFirstReadStream(Jpeg());

        await Assert.ThrowsAsync<IOException>(() => storage.StageAsync(
            [new MediaUpload(input, "image/jpeg")],
            CancellationToken.None));

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Fact]
    public async Task CleanupFailurePreservesOriginalInputFailureAndCleanupFailure()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var outside = Path.Combine(Path.GetTempPath(), "toystore-cleanup-outside", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outside);
        await File.WriteAllTextAsync(
            Path.Combine(outside, "sentinel"),
            "keep",
            TestContext.Current.CancellationToken);
        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        await using var input = new ThrowAfterFirstReadStream(Jpeg(), () =>
        {
            var batch = Directory.EnumerateDirectories(Path.Combine(root, ".staging")).Single();
            File.CreateSymbolicLink(Path.Combine(batch, "outside.jpg"), Path.Combine(outside, "sentinel"));
        });

        var exception = await Assert.ThrowsAsync<AggregateException>(() => storage.StageAsync(
            [new MediaUpload(input, "image/jpeg")],
            CancellationToken.None));

        Assert.Contains(exception.InnerExceptions, error => error.Message.Contains("input failed", StringComparison.Ordinal));
        Assert.Contains(exception.InnerExceptions, error => error.Message.Contains("Symbolic links", StringComparison.Ordinal));
        Assert.True(File.Exists(Path.Combine(outside, "sentinel")));
        Directory.Delete(Path.Combine(root, ".staging"), true);
        Directory.CreateDirectory(Path.Combine(root, ".staging"));
        Directory.Delete(outside, true);
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

    internal static byte[] Jpeg() => Image(SKEncodedImageFormat.Jpeg);
    internal static byte[] Png() => Image(SKEncodedImageFormat.Png);

    private static byte[] Image(SKEncodedImageFormat format)
    {
        using var bitmap = new SKBitmap(2, 2);
        bitmap.Erase(SKColors.Lime);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(format, 90);
        return data.ToArray();
    }

    private sealed class CancelAfterFirstReadStream(
        byte[] bytes,
        CancellationTokenSource cancellation) : Stream
    {
        private bool read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (read)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            read = true;
            bytes.CopyTo(buffer);
            cancellation.Cancel();
            return ValueTask.FromResult(bytes.Length);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class ThrowAfterFirstReadStream(byte[] bytes, Action? beforeThrow = null) : Stream
    {
        private bool read;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (read)
            {
                beforeThrow?.Invoke();
                throw new IOException("input failed");
            }

            read = true;
            bytes.CopyTo(buffer);
            return ValueTask.FromResult(bytes.Length);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    private sealed class BarrierCollisionGenerator : IMediaIdGenerator, IDisposable
    {
        private const string CollisionId = "aabbccddeeff00112233445566778899";
        private readonly Barrier barrier = new(2);
        private readonly MediaIdGenerator random = new();
        private int calls;

        public string CreateId()
        {
            var call = Interlocked.Increment(ref calls);
            if (call == 1)
            {
                return random.CreateId();
            }

            if (call is 2 or 3)
            {
                barrier.SignalAndWait(TestContext.Current.CancellationToken);
                return CollisionId;
            }

            return random.CreateId();
        }

        public void Dispose() => barrier.Dispose();
    }

    private sealed class SequenceGenerator(params string[] ids) : IMediaIdGenerator
    {
        private readonly Queue<string> remaining = new(ids);

        public string CreateId() => remaining.Count > 0
            ? remaining.Dequeue()
            : new MediaIdGenerator().CreateId();
    }
}
