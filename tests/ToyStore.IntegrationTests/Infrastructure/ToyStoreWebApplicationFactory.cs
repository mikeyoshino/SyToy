using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace ToyStore.IntegrationTests.Infrastructure;

public class ToyStoreWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string connectionString;
    private readonly string dataProtectionApplicationName;
    private readonly string dataProtectionKeysPath;
    private readonly bool ownsDataProtectionKeysPath;
    private readonly string storageRootPath;

    public string StorageRootPath => storageRootPath;

    public ToyStoreWebApplicationFactory(string connectionString)
        : this(connectionString, dataProtectionKeysPath: null, "ToyStore")
    {
    }

    public ToyStoreWebApplicationFactory(
        string connectionString,
        string? dataProtectionKeysPath,
        string dataProtectionApplicationName)
    {
        if (!PostgreSqlFixture.IsSafeTestDatabase(connectionString))
        {
            throw new ArgumentException(
                "Web application factory requires an explicitly named test database.",
                nameof(connectionString));
        }

        this.connectionString = connectionString;
        this.dataProtectionApplicationName =
            !string.IsNullOrWhiteSpace(dataProtectionApplicationName)
                ? dataProtectionApplicationName
                : throw new ArgumentException(
                    "Data Protection application name is required.",
                    nameof(dataProtectionApplicationName));
        ownsDataProtectionKeysPath = string.IsNullOrWhiteSpace(dataProtectionKeysPath);
        this.dataProtectionKeysPath = ownsDataProtectionKeysPath
            ? Path.Combine(
                Path.GetTempPath(),
                "toystore-integration-tests",
                Guid.NewGuid().ToString("N"))
            : Path.GetFullPath(dataProtectionKeysPath!);
        storageRootPath = Path.Combine(
            Path.GetTempPath(),
            "toystore-integration-media",
            Guid.NewGuid().ToString("N"));
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", connectionString);
        builder.UseSetting("DataProtection:KeysPath", dataProtectionKeysPath);
        builder.UseSetting("DataProtection:ApplicationName", dataProtectionApplicationName);
        builder.UseSetting("Storage:RootPath", storageRootPath);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing
            && ownsDataProtectionKeysPath
            && Directory.Exists(dataProtectionKeysPath))
        {
            Directory.Delete(dataProtectionKeysPath, recursive: true);
        }

        if (disposing && Directory.Exists(storageRootPath))
        {
            Directory.Delete(storageRootPath, recursive: true);
        }
    }
}
