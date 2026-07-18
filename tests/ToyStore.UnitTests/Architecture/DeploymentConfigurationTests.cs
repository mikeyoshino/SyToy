namespace ToyStore.UnitTests.Architecture;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void SystemdServiceProvidesWritablePersistentLogPath()
    {
        var repositoryRoot = FindRepositoryRoot();
        var service = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "toystore.service.example"));

        Assert.Contains(
            "Environment=Serilog__WriteTo__1__Args__path=/var/lib/toystore/logs/toystore-.log",
            service,
            StringComparison.Ordinal);
        Assert.Contains(
            "StateDirectory=toystore/logs toystore/keys",
            service,
            StringComparison.Ordinal);
        Assert.Contains("StateDirectoryMode=0750", service, StringComparison.Ordinal);
        Assert.Contains(
            "ReadWritePaths=/var/lib/toystore/uploads /var/lib/toystore/keys /var/lib/toystore/logs",
            service,
            StringComparison.Ordinal);
    }

    [Fact]
    public void DocumentationDefinesPersistentDataProtectionAndStartupMigrations()
    {
        var repositoryRoot = FindRepositoryRoot();
        var deployment = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "DEPLOYMENT.md"));
        var localDevelopment = File.ReadAllText(
            Path.Combine(repositoryRoot, "docs", "LOCAL_DEVELOPMENT.md"));

        Assert.Contains(
            "DataProtection__KeysPath=/var/lib/toystore/keys",
            deployment,
            StringComparison.Ordinal);
        Assert.Contains("EF Core Code First", localDevelopment, StringComparison.Ordinal);
        Assert.Contains("Database.MigrateAsync()", localDevelopment, StringComparison.Ordinal);
    }

    [Fact]
    public void ActiveIdentityDocumentationUsesOnlyCustomerAndAdminRoles()
    {
        var repositoryRoot = FindRepositoryRoot();
        var tasks = File.ReadAllText(Path.Combine(repositoryRoot, "TASKS.md"));
        var architecture = File.ReadAllText(Path.Combine(repositoryRoot, "docs", "ARCHITECTURE.md"));

        Assert.Contains("Roles: `Customer`, `Admin`", architecture, StringComparison.Ordinal);
        Assert.Contains("Roles: Customer, Admin", tasks, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer, Staff, Admin", architecture, StringComparison.Ordinal);
        Assert.DoesNotContain("Customer, Staff, Admin", tasks, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate the directory containing ToyStore.sln.");
    }
}
