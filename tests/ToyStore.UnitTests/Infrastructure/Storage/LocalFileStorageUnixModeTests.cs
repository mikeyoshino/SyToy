using System.Runtime.Versioning;
using Microsoft.Extensions.Options;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageUnixModeTests : IDisposable
{
    private static readonly UnixFileMode ApprovedDirectoryMode =
        UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
        UnixFileMode.GroupRead | UnixFileMode.GroupExecute;

    private readonly string root = Path.Combine(
        Path.GetTempPath(),
        "toystore-mode-tests",
        Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExistingWorldAccessibleRootIsRejectedWithoutChangingModeOrContent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateDirectory(root, (UnixFileMode)0x1ff);
        CreateDirectory(Path.Combine(root, ".staging"), ApprovedDirectoryMode);
        CreateDirectory(Path.Combine(root, "files"), ApprovedDirectoryMode);
        var sentinel = Path.Combine(root, "operator-content");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        using var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            storage.InitializeAsync(CancellationToken.None));

        Assert.Contains("permissions", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal((UnixFileMode)0x1ff, File.GetUnixFileMode(root));
        Assert.Equal("keep", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(Path.Combine(root, ".staging")));
        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(Path.Combine(root, "files")));
    }

    [Theory]
    [InlineData(0x10)] // group write
    [InlineData(0x04)] // other read
    [InlineData(0x02)] // other write
    [InlineData(0x01)] // other execute
    public async Task ExistingRootRejectsEachDisallowedPermission(int disallowedPermission)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var mode = ApprovedDirectoryMode | (UnixFileMode)disallowedPermission;
        CreateDirectory(root, mode);
        using var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            storage.InitializeAsync(CancellationToken.None));

        Assert.Contains("permissions", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(mode, File.GetUnixFileMode(root));
    }

    [Theory]
    [InlineData(".staging")]
    [InlineData("files")]
    public async Task ExistingWorldAccessibleFixedChildIsRejectedWithoutChangingModeOrContent(
        string childName)
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateDirectory(root, ApprovedDirectoryMode);
        CreateDirectory(Path.Combine(root, ".staging"), ApprovedDirectoryMode);
        CreateDirectory(Path.Combine(root, "files"), ApprovedDirectoryMode);
        var child = Path.Combine(root, childName);
        File.SetUnixFileMode(child, (UnixFileMode)0x1ff);
        var sentinelDirectory = Path.Combine(child, "operator-content");
        Directory.CreateDirectory(sentinelDirectory);
        var sentinel = Path.Combine(sentinelDirectory, "sentinel");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        using var storage = CreateStorage();

        var exception = await Assert.ThrowsAsync<IOException>(() =>
            storage.InitializeAsync(CancellationToken.None));

        Assert.Contains("permissions", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(root));
        Assert.Equal((UnixFileMode)0x1ff, File.GetUnixFileMode(child));
        Assert.Equal("keep", await File.ReadAllTextAsync(
            sentinel,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExistingRootAndFixedChildrenAtApprovedModeInitializeSuccessfully()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        CreateDirectory(root, ApprovedDirectoryMode);
        CreateDirectory(Path.Combine(root, ".staging"), ApprovedDirectoryMode);
        CreateDirectory(Path.Combine(root, "files"), ApprovedDirectoryMode);
        using var storage = CreateStorage();

        await storage.InitializeAsync(CancellationToken.None);

        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(root));
        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(Path.Combine(root, ".staging")));
        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(Path.Combine(root, "files")));
    }

    [Fact]
    public async Task MissingRootIsCreatedAtomicallyWithApprovedMode()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        Assert.False(Directory.Exists(root));
        using var storage = CreateStorage();

        await storage.InitializeAsync(CancellationToken.None);

        Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(root));
    }

    [Fact]
    public async Task MissingRootCreationDoesNotChangeExistingAncestorModeOrContent()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var ancestor = root + "-ancestor";
        var storageRoot = Path.Combine(ancestor, "uploads");
        CreateDirectory(ancestor, (UnixFileMode)0x1ff);
        var sentinel = Path.Combine(ancestor, "operator-content");
        await File.WriteAllTextAsync(sentinel, "keep", TestContext.Current.CancellationToken);
        try
        {
            using var storage = CreateStorage(storageRoot);

            await storage.InitializeAsync(CancellationToken.None);

            Assert.Equal((UnixFileMode)0x1ff, File.GetUnixFileMode(ancestor));
            Assert.Equal("keep", await File.ReadAllTextAsync(
                sentinel,
                TestContext.Current.CancellationToken));
            Assert.Equal(ApprovedDirectoryMode, File.GetUnixFileMode(storageRoot));
        }
        finally
        {
            Directory.Delete(ancestor, recursive: true);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(root))
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private LocalFileStorage CreateStorage(string? storageRoot = null) => new(
        Options.Create(new LocalFileStorageOptions { RootPath = storageRoot ?? root }),
        TimeProvider.System);

    [UnsupportedOSPlatform("windows")]
    private static void CreateDirectory(string path, UnixFileMode mode)
    {
        Directory.CreateDirectory(path);
        File.SetUnixFileMode(path, mode);
    }
}
