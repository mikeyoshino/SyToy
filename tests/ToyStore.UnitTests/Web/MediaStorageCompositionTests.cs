namespace ToyStore.UnitTests.Web;

public sealed class MediaStorageCompositionTests
{
    [Fact]
    public void ProgramInitializesStorageBeforeBootstrapExitAndMapsProviderNeutralEndpoint()
    {
        var webRoot = Path.Combine(FindRepositoryRoot(), "src", "ToyStore.Web");
        var program = File.ReadAllText(Path.Combine(webRoot, "Program.cs"));
        var initialize = program.IndexOf("InitializeFileStorageAsync", StringComparison.Ordinal);
        var bootstrapExit = program.IndexOf("if (bootstrapAdminRequested)", StringComparison.Ordinal);
        var endpoint = File.ReadAllText(Path.Combine(webRoot, "Media", "MediaEndpointExtensions.cs"));

        Assert.True(initialize >= 0 && bootstrapExit > initialize);
        Assert.Contains("IFileStorage", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalFile", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("PhysicalFileProvider", endpoint, StringComparison.Ordinal);
        Assert.DoesNotContain("Storage:RootPath", endpoint, StringComparison.Ordinal);
        Assert.Contains("AllowAnonymous", endpoint, StringComparison.Ordinal);
    }

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
}
