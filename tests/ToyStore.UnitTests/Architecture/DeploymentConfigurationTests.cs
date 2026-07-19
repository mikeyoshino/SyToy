namespace ToyStore.UnitTests.Architecture;

public sealed class DeploymentConfigurationTests
{
    [Fact]
    public void CaddyTerminatesTlsForWwwAndRedirectsToTheCanonicalApexHost()
    {
        var repositoryRoot = FindRepositoryRoot();
        var caddyfile = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "Caddyfile"));

        Assert.Contains("www.{$TOYSTORE_DOMAIN} {", caddyfile, StringComparison.Ordinal);
        Assert.Contains(
            "redir https://{$TOYSTORE_DOMAIN}{uri} permanent",
            caddyfile,
            StringComparison.Ordinal);
        var canonicalBlockIndex = caddyfile.IndexOf(
            "\n{$TOYSTORE_DOMAIN} {",
            StringComparison.Ordinal);
        Assert.True(canonicalBlockIndex > 0, "Missing canonical apex Caddy site block.");
    }

    [Fact]
    public void ProductionImageRunsAsNonRootAndKeepsRuntimeStateOutsideTheImage()
    {
        var repositoryRoot = FindRepositoryRoot();
        var dockerfile = File.ReadAllText(Path.Combine(repositoryRoot, "Dockerfile"));
        var compose = File.ReadAllText(
            Path.Combine(repositoryRoot, "deploy", "compose.production.yaml"));

        Assert.Contains("FROM mcr.microsoft.com/dotnet/aspnet:10.0", dockerfile, StringComparison.Ordinal);
        Assert.Contains("USER app", dockerfile, StringComparison.Ordinal);
        Assert.Contains("DataProtection__KeysPath: /var/lib/toystore/keys", compose, StringComparison.Ordinal);
        Assert.Contains("Storage__RootPath: /var/lib/toystore/uploads", compose, StringComparison.Ordinal);
        Assert.Contains("/var/lib/toystore/logs:/var/lib/toystore/logs", compose, StringComparison.Ordinal);
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
    public void ProductionWorkflowRequiresManualBranchInputAndBuildGatesBeforeDigestDeployment()
    {
        var repositoryRoot = FindRepositoryRoot();
        var workflow = File.ReadAllText(Path.Combine(
            repositoryRoot,
            ".github",
            "workflows",
            "deploy-production.yml"));

        Assert.Contains("workflow_dispatch:", workflow, StringComparison.Ordinal);
        Assert.Contains("branch:", workflow, StringComparison.Ordinal);
        Assert.Contains("default: main", workflow, StringComparison.Ordinal);
        Assert.Contains("environment: production", workflow, StringComparison.Ordinal);
        Assert.Contains("cancel-in-progress: false", workflow, StringComparison.Ordinal);
        Assert.Contains("packages: write", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("dotnet test", workflow, StringComparison.Ordinal);
        AssertAppearsBefore(workflow, "Build release", "Build and push production image");
        AssertAppearsBefore(workflow, "Generate migration review artifact", "Build and push production image");
        AssertAppearsBefore(workflow, "Build and push production image", "Deploy immutable image on VPS");
        Assert.Contains("docker/build-push-action@v6", workflow, StringComparison.Ordinal);
        Assert.Contains("steps.build.outputs.digest", workflow, StringComparison.Ordinal);
        Assert.Contains("VPS_SSH_KNOWN_HOSTS", workflow, StringComparison.Ordinal);
        Assert.Contains("sudo /usr/local/sbin/toystore-deploy", workflow, StringComparison.Ordinal);
        Assert.Contains("/health/ready", workflow, StringComparison.Ordinal);
        Assert.DoesNotContain("StrictHostKeyChecking=no", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Stripe", workflow, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ConnectionStrings", workflow, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ProductionDeployCommandBacksUpStatePinsImageDigestAndRollsBackFailedReadiness()
    {
        var repositoryRoot = FindRepositoryRoot();
        var deployCommand = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "toystore-deploy"));
        var compose = File.ReadAllText(Path.Combine(repositoryRoot, "deploy", "compose.production.yaml"));

        Assert.Contains("@sha256:[0-9a-f]{64}", deployCommand, StringComparison.Ordinal);
        Assert.Contains("flock -n", deployCommand, StringComparison.Ordinal);
        Assert.Contains("pg_dump --format=custom", deployCommand, StringComparison.Ordinal);
        Assert.Contains("uploads/files keys", deployCommand, StringComparison.Ordinal);
        Assert.Contains("compose pull web caddy postgres", deployCommand, StringComparison.Ordinal);
        Assert.Contains("compose up -d --remove-orphans", deployCommand, StringComparison.Ordinal);
        Assert.Contains("/health/ready", deployCommand, StringComparison.Ordinal);
        Assert.Contains("current_image", deployCommand, StringComparison.Ordinal);
        Assert.Contains("schema rollback is intentionally manual", deployCommand, StringComparison.Ordinal);
        Assert.Contains("127.0.0.1:5000:8080", compose, StringComparison.Ordinal);
        Assert.Contains("toystore-production-postgres-data", compose, StringComparison.Ordinal);
        Assert.Contains("internal: true", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("5432:5432", compose, StringComparison.Ordinal);
        Assert.DoesNotContain("toystore_dev", compose, StringComparison.Ordinal);
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

    private static void AssertAppearsBefore(string source, string first, string second)
    {
        var firstIndex = source.IndexOf(first, StringComparison.Ordinal);
        var secondIndex = source.IndexOf(second, StringComparison.Ordinal);

        Assert.True(firstIndex >= 0, $"Missing expected text: {first}");
        Assert.True(secondIndex >= 0, $"Missing expected text: {second}");
        Assert.True(firstIndex < secondIndex, $"Expected '{first}' before '{second}'.");
    }
}
