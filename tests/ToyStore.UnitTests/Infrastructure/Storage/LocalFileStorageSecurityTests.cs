using Microsoft.Extensions.Options;
using ToyStore.Application.Common.Files;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageSecurityTests : IDisposable
{
    private const string Batch = "aabbccddeeff00112233445566778899";
    private const string NextBatch = "11223344556677889900aabbccddeeff";
    private const string FileId = "00112233445566778899aabbccddeeff";
    private readonly string root = Path.Combine(Path.GetTempPath(), "toystore-media-security", Guid.NewGuid().ToString("N"));
    private readonly string outside = Path.Combine(Path.GetTempPath(), "toystore-media-outside", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task InitializeCancellationRemovesProbeBeforeRetry()
    {
        using var cancellation = new CancellationTokenSource();
        using var storage = CreateStorage(new CallbackIdGenerator(() => cancellation.Cancel()));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            storage.InitializeAsync(cancellation.Token));

        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
    }

    [Fact]
    public async Task ExistingBatchCollisionIsNotReusedOrDeleted()
    {
        using var storage = CreateStorage(new SequenceIdGenerator(
            Guid.NewGuid().ToString("N"),
            Batch,
            NextBatch,
            FileId));
        await storage.InitializeAsync(CancellationToken.None);
        var collision = Path.Combine(root, ".staging", Batch);
        Directory.CreateDirectory(collision);
        await File.WriteAllTextAsync(
            Path.Combine(collision, "sentinel"),
            "keep",
            TestContext.Current.CancellationToken);

        var result = await storage.StageAsync(
            [new MediaUpload(new MemoryStream(LocalFileStorageStageTests.Jpeg()), "image/jpeg")],
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(NextBatch, result.Value.BatchToken);
        Assert.True(File.Exists(Path.Combine(collision, "sentinel")));
    }

    [Fact]
    public async Task DiscardRefusesAChildSymlinkWithoutTouchingOutsideTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var batch = Path.Combine(root, ".staging", Batch);
        Directory.CreateDirectory(batch);
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(Path.Combine(batch, "link"), outside);

        await Assert.ThrowsAsync<IOException>(() =>
            storage.DiscardStagingAsync(Batch, CancellationToken.None));

        Assert.True(File.Exists(sentinel));
        Assert.True(Directory.Exists(batch));
    }

    [Fact]
    public async Task StageRefusesASymlinkBatchCollisionWithoutFollowingIt()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage(new SequenceIdGenerator(
            Guid.NewGuid().ToString("N"),
            Batch));
        await storage.InitializeAsync(CancellationToken.None);
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        Directory.CreateSymbolicLink(Path.Combine(root, ".staging", Batch), outside);

        await Assert.ThrowsAsync<IOException>(() => storage.StageAsync(
            [new MediaUpload(new MemoryStream(LocalFileStorageStageTests.Jpeg()), "image/jpeg")],
            CancellationToken.None));

        Assert.True(File.Exists(sentinel));
    }

    [Fact]
    public async Task StaleCleanupRefusesAChildSymlinkWithoutTouchingOutsideTarget()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var batch = Path.Combine(root, ".staging", Batch);
        Directory.CreateDirectory(batch);
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(Path.Combine(batch, "link.jpg"), sentinel);
        Directory.SetLastWriteTimeUtc(batch, DateTime.UtcNow.AddDays(-2));

        await Assert.ThrowsAsync<IOException>(() => storage.CleanupStagingAsync(
            DateTimeOffset.UtcNow.AddHours(-24),
            CancellationToken.None));

        Assert.True(File.Exists(sentinel));
        Assert.True(Directory.Exists(batch));
    }

    [Fact]
    public async Task OpenReadReturnsNullForSymlinkInsteadOfLeakingAStorageError()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var storage = CreateStorage();
        await storage.InitializeAsync(CancellationToken.None);
        var batch = Path.Combine(root, "files", Batch);
        Directory.CreateDirectory(batch);
        Directory.CreateDirectory(outside);
        var outsideFile = Path.Combine(outside, "outside.jpg");
        await File.WriteAllBytesAsync(
            outsideFile,
            LocalFileStorageStageTests.Jpeg(),
            TestContext.Current.CancellationToken);
        File.CreateSymbolicLink(Path.Combine(batch, FileId + ".jpg"), outsideFile);

        var result = await storage.OpenReadAsync(
            $"{Batch}/{FileId}.jpg",
            CancellationToken.None);

        Assert.Null(result);
    }

    [Theory]
    [InlineData(".staging")]
    [InlineData("files")]
    public async Task ExistingFixedChildSymlinkIsRejectedBeforeOutsideModeOrContentChanges(
        string childName)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Directory.CreateDirectory(root);
        File.SetUnixFileMode(
            root,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute);
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        var originalMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute;
        File.SetUnixFileMode(outside, originalMode);
        Directory.CreateSymbolicLink(Path.Combine(root, childName), outside);
        using var storage = CreateStorage();

        await Assert.ThrowsAsync<IOException>(() => storage.InitializeAsync(CancellationToken.None));

        Assert.Equal(originalMode, File.GetUnixFileMode(outside));
        Assert.Equal("keep", await File.ReadAllTextAsync(sentinel, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StableAncestorAliasIsCanonicalizedWithoutRejectingMacOsStyleParentAliases()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = Path.Combine(Path.GetTempPath(), "toystore-root-alias", Guid.NewGuid().ToString("N"));
        var actual = Path.Combine(parent, "actual");
        var alias = Path.Combine(parent, "alias");
        Directory.CreateDirectory(actual);
        Directory.CreateSymbolicLink(alias, actual);
        try
        {
            using var storage = new LocalFileStorage(
                Options.Create(new LocalFileStorageOptions
                {
                    RootPath = Path.Combine(alias, "uploads"),
                }),
                TimeProvider.System);

            await storage.InitializeAsync(CancellationToken.None);

            Assert.True(Directory.Exists(Path.Combine(actual, "uploads", ".staging")));
            Assert.True(Directory.Exists(Path.Combine(actual, "uploads", "files")));
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void ReadAndCommitVerificationRequestTheCompleteSixteenByteHeader()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ToyStore.Infrastructure",
            "Storage",
            "LocalFileStorage.cs"));

        Assert.Equal(
            2,
            source.Split(
                "ReadAtLeast(header, header.Length, throwOnEndOfStream: false)",
                StringSplitOptions.None).Length - 1);
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, true);
        }

        if (Directory.Exists(outside))
        {
            Directory.Delete(outside, true);
        }
    }

    private LocalFileStorage CreateStorage(IMediaIdGenerator? generator = null) => generator is null
        ? new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = root }),
            TimeProvider.System)
        : new LocalFileStorage(
            Options.Create(new LocalFileStorageOptions { RootPath = root }),
            TimeProvider.System,
            generator);

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private sealed class SequenceIdGenerator(params string[] ids) : IMediaIdGenerator
    {
        private readonly Queue<string> remaining = new(ids);

        public string CreateId() => remaining.Count > 0 ? remaining.Dequeue() : Guid.NewGuid().ToString("N");
    }

    private sealed class CallbackIdGenerator(Action callback) : IMediaIdGenerator
    {
        public string CreateId()
        {
            callback();
            return Guid.NewGuid().ToString("N");
        }
    }
}
