using Microsoft.Extensions.Options;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageStartupProbeTests : IDisposable
{
    private const string ProbeOne = "aabbccddeeff00112233445566778899";
    private const string ProbeTwo = "11223344556677889900aabbccddeeff";
    private readonly string root = Path.Combine(Path.GetTempPath(), "toystore-probe-tests", Guid.NewGuid().ToString("N"));
    private readonly string outside = Path.Combine(Path.GetTempPath(), "toystore-probe-outside", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task StartupRecoversCrashArtifactsFromStagingAndCommittedSides()
    {
        CreateFixedChildren();
        CreateProbeArtifact(Path.Combine(root, ".staging"), ProbeOne);
        CreateProbeArtifact(Path.Combine(root, "files"), ProbeTwo);
        using var storage = CreateStorage();

        await storage.InitializeAsync(CancellationToken.None);

        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")),
            path => Path.GetFileName(path).StartsWith(".probe-", StringComparison.Ordinal));
        Assert.DoesNotContain(
            Directory.EnumerateFileSystemEntries(Path.Combine(root, "files")),
            path => Path.GetFileName(path).StartsWith(".probe-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task DestinationCollisionFailsStartupCleansBothSidesAndNextRetryRecovers()
    {
        CreateFixedChildren();
        using var storage = CreateStorage(new FixedCallbackGenerator(ProbeOne, () =>
            CreateProbeArtifact(Path.Combine(root, "files"), ProbeOne)));

        await Assert.ThrowsAsync<IOException>(() =>
            storage.InitializeAsync(CancellationToken.None));

        Assert.False(Directory.Exists(Path.Combine(root, ".staging", ".probe-" + ProbeOne)));
        Assert.False(Directory.Exists(Path.Combine(root, "files", ".probe-" + ProbeOne)));
        using var retry = CreateStorage();
        await retry.InitializeAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ProbeFailurePreservesOriginalAndSecureCleanupFailure()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateFixedChildren();
        Directory.CreateDirectory(outside);
        var sentinel = Path.Combine(outside, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        using var storage = CreateStorage(new FixedCallbackGenerator(ProbeOne, () =>
        {
            var collision = Path.Combine(root, "files", ".probe-" + ProbeOne);
            Directory.CreateDirectory(collision);
            File.CreateSymbolicLink(Path.Combine(collision, "outside"), sentinel);
        }));

        var exception = await Assert.ThrowsAsync<AggregateException>(() =>
            storage.InitializeAsync(CancellationToken.None));

        Assert.Contains(exception.InnerExceptions, error =>
            error.Message.Contains("occupied", StringComparison.Ordinal));
        Assert.Contains(exception.InnerExceptions, error =>
            error.Message.Contains("Symbolic links", StringComparison.Ordinal));
        Assert.True(File.Exists(sentinel));
        File.Delete(Path.Combine(root, "files", ".probe-" + ProbeOne, "outside"));
        Directory.Delete(Path.Combine(root, "files", ".probe-" + ProbeOne));
    }

    [Fact]
    public async Task UnwritableCommittedDirectoryFailsRenameAndRetryRecovers()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateFixedChildren();
        var files = Path.Combine(root, "files");
        File.SetUnixFileMode(
            files,
            UnixFileMode.UserRead | UnixFileMode.UserExecute);
        try
        {
            using var storage = CreateStorage(new FixedCallbackGenerator(ProbeOne));

            await Assert.ThrowsAnyAsync<Exception>(() =>
                storage.InitializeAsync(CancellationToken.None));

            Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(root, ".staging")));
            Assert.Empty(Directory.EnumerateFileSystemEntries(files));
        }
        finally
        {
            File.SetUnixFileMode(
                files,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        using var retry = CreateStorage();
        await retry.InitializeAsync(CancellationToken.None);
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

    private void CreateFixedChildren()
    {
        Directory.CreateDirectory(Path.Combine(root, ".staging"));
        Directory.CreateDirectory(Path.Combine(root, "files"));
        if (!OperatingSystem.IsWindows())
        {
            var mode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
                       UnixFileMode.GroupRead | UnixFileMode.GroupExecute;
            File.SetUnixFileMode(root, mode);
            File.SetUnixFileMode(Path.Combine(root, ".staging"), mode);
            File.SetUnixFileMode(Path.Combine(root, "files"), mode);
        }
    }

    private static void CreateProbeArtifact(string parent, string id)
    {
        var artifact = Path.Combine(parent, ".probe-" + id);
        Directory.CreateDirectory(artifact);
        File.WriteAllBytes(Path.Combine(artifact, "probe.bin"), [0x01]);
    }

    private sealed class FixedCallbackGenerator(string id, Action? callback = null) : IMediaIdGenerator
    {
        private int invoked;

        public string CreateId()
        {
            if (Interlocked.Exchange(ref invoked, 1) == 0)
            {
                callback?.Invoke();
                return id;
            }

            return new MediaIdGenerator().CreateId();
        }
    }
}
