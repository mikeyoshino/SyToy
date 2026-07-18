using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure.Storage;

public sealed class LocalFileStorageOptionsValidatorTests
{
    [Fact]
    public void RejectsRelativeRootAndNonPositiveRetention()
    {
        var validator = new LocalFileStorageOptionsValidator(new TestEnvironment());

        Assert.True(validator.Validate(null, new() { RootPath = ".data/uploads" }).Failed);
        Assert.True(validator.Validate(null, new()
        {
            RootPath = Path.GetTempPath(),
            StagingRetention = TimeSpan.Zero,
        }).Failed);
    }

    [Fact]
    public void ProductionRejectsRootInsideDeploymentButAcceptsPersistentExternalRoot()
    {
        var deployment = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "current");
        var validator = new LocalFileStorageOptionsValidator(new TestEnvironment
        {
            EnvironmentName = Environments.Production,
            ContentRootPath = deployment,
        });

        var inside = validator.Validate(null, new()
        {
            RootPath = Path.Combine(deployment, "uploads"),
        });
        var outside = validator.Validate(null, new()
        {
            RootPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "uploads"),
        });

        Assert.True(inside.Failed);
        Assert.True(outside.Succeeded);
    }

    [Fact]
    public void ProductionComparisonResolvesAncestorAliasAndCannotBypassDeploymentRoot()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        var parent = Path.Combine(Path.GetTempPath(), "toystore-alias-options", Guid.NewGuid().ToString("N"));
        var deployment = Path.Combine(parent, "deployment");
        var alias = Path.Combine(parent, "alias");
        Directory.CreateDirectory(deployment);
        Directory.CreateSymbolicLink(alias, deployment);
        try
        {
            var validator = new LocalFileStorageOptionsValidator(new TestEnvironment
            {
                EnvironmentName = Environments.Production,
                ContentRootPath = deployment,
            });

            var result = validator.Validate(null, new()
            {
                RootPath = Path.Combine(alias, "uploads"),
            });

            Assert.Equal(
                Path.Combine(
                    StoragePathResolver.ResolveExistingAliases(deployment, includeLeaf: true),
                    "uploads"),
                StoragePathResolver.ResolveExistingAliases(
                    Path.Combine(alias, "uploads"),
                    includeLeaf: false));
            Assert.True(result.Failed);
        }
        finally
        {
            Directory.Delete(parent, true);
        }
    }

    private sealed class TestEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ToyStore.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
