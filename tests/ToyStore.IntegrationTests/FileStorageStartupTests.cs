using Microsoft.AspNetCore.Hosting;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class FileStorageStartupTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public void OrdinaryStartupInitializesAnIsolatedPersistentTree()
    {
        using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();

        Assert.True(Directory.Exists(Path.Combine(factory.StorageRootPath, ".staging")));
        Assert.True(Directory.Exists(Path.Combine(factory.StorageRootPath, "files")));
        Assert.Empty(Directory.EnumerateFileSystemEntries(
            Path.Combine(factory.StorageRootPath, ".staging")));
    }

    [Fact]
    public void RootSymlinkPreventsStartup()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = Path.Combine(Path.GetTempPath(), "toystore-storage-startup", Guid.NewGuid().ToString("N"));
        var target = Path.Combine(parent, "target");
        var link = Path.Combine(parent, "link");
        Directory.CreateDirectory(target);
        Directory.CreateSymbolicLink(link, target);

        try
        {
            using var factory = new StorageRootFactory(postgreSql.ConnectionString, link);

            Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    private sealed class StorageRootFactory(string connectionString, string storageRoot)
        : ToyStoreWebApplicationFactory(connectionString)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Storage:RootPath", storageRoot);
        }
    }
}
