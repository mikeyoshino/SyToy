using System.Xml.Linq;

namespace ToyStore.UnitTests.Architecture;

public sealed class ProjectDependencyTests
{
    public static TheoryData<string, string[]> ApprovedDependencies => new()
    {
        { "src/ToyStore.Domain/ToyStore.Domain.csproj", [] },
        {
            "src/ToyStore.Application/ToyStore.Application.csproj",
            ["src/ToyStore.Domain/ToyStore.Domain.csproj"]
        },
        {
            "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            [
                "src/ToyStore.Application/ToyStore.Application.csproj",
                "src/ToyStore.Domain/ToyStore.Domain.csproj",
            ]
        },
        {
            "src/ToyStore.Web/ToyStore.Web.csproj",
            [
                "src/ToyStore.Application/ToyStore.Application.csproj",
                "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            ]
        },
    };

    [Theory]
    [MemberData(nameof(ApprovedDependencies))]
    public void ProjectReferencesMatchTheApprovedDependencyGraph(
        string projectPath,
        string[] expectedReferences)
    {
        var solutionRoot = FindSolutionRoot();
        var absoluteProjectPath = Path.Combine(solutionRoot, projectPath);
        var projectDirectory = Path.GetDirectoryName(absoluteProjectPath)!;
        var document = XDocument.Load(absoluteProjectPath);

        var actualReferences = document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => include is not null)
            .Select(include => include!
                .Replace('\\', Path.DirectorySeparatorChar)
                .Replace('/', Path.DirectorySeparatorChar))
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include)))
            .Select(path => Path.GetRelativePath(solutionRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.Order(StringComparer.Ordinal), actualReferences);
    }

    private static string FindSolutionRoot()
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

        throw new DirectoryNotFoundException(
            "Could not locate the directory containing ToyStore.sln.");
    }
}
