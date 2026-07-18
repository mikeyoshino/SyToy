using System.Xml.Linq;

namespace ToyStore.UnitTests.Architecture;

public sealed class PackageDependencyTests
{
    public static TheoryData<string, string, string> RequiredPackages => new()
    {
        { "src/ToyStore.Application/ToyStore.Application.csproj", "MediatR", "12.5.0" },
        {
            "src/ToyStore.Application/ToyStore.Application.csproj",
            "FluentValidation.DependencyInjectionExtensions",
            "12.1.1"
        },
        {
            "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            "Microsoft.AspNetCore.Identity.EntityFrameworkCore",
            "10.0.10"
        },
        {
            "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            "Microsoft.EntityFrameworkCore.Design",
            "10.0.10"
        },
        {
            "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            "Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore",
            "10.0.10"
        },
        {
            "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj",
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            "10.0.3"
        },
        { "src/ToyStore.Web/ToyStore.Web.csproj", "Serilog.AspNetCore", "10.0.0" },
        {
            "src/ToyStore.Web/ToyStore.Web.csproj",
            "Microsoft.EntityFrameworkCore.Design",
            "10.0.10"
        },
        { "tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj", "xunit.v3", "3.2.2" },
        { "tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj", "xunit.v3", "3.2.2" },
        {
            "tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj",
            "Testcontainers.PostgreSql",
            "4.13.0"
        },
        {
            "tests/ToyStore.IntegrationTests/ToyStore.IntegrationTests.csproj",
            "Respawn",
            "7.0.0"
        },
    };

    [Theory]
    [MemberData(nameof(RequiredPackages))]
    public void ProjectHasRequiredDirectPackageReference(
        string projectPath,
        string packageName,
        string expectedVersion)
    {
        var solutionRoot = FindSolutionRoot();
        var normalizedProjectPath = projectPath
            .Replace('\\', Path.DirectorySeparatorChar)
            .Replace('/', Path.DirectorySeparatorChar);
        var absoluteProjectPath = Path.GetFullPath(Path.Combine(solutionRoot, normalizedProjectPath));
        var document = XDocument.Load(absoluteProjectPath);

        var directPackages = document.Root!
            .Elements("ItemGroup")
            .Elements("PackageReference")
            .Select(reference => new
            {
                Name = reference.Attribute("Include")?.Value,
                Version = reference.Attribute("Version")?.Value,
            })
            .Where(package => package.Name is not null)
            .ToDictionary(
                package => package.Name!,
                package => package.Version,
                StringComparer.OrdinalIgnoreCase);

        Assert.True(
            directPackages.TryGetValue(packageName, out var actualVersion),
            $"Expected '{projectPath}' to directly reference '{packageName}' version " +
            $"'{expectedVersion}'. Direct references: {FormatPackages(directPackages)}");
        Assert.True(
            string.Equals(expectedVersion, actualVersion, StringComparison.Ordinal),
            $"Expected direct package '{packageName}' in '{projectPath}' to use version " +
            $"'{expectedVersion}', but found '{actualVersion ?? "<missing>"}'.");
    }

    [Fact]
    public void ProductionProjectsDoNotReferenceSqlite()
    {
        var solutionRoot = FindSolutionRoot();
        var productionProjects = Directory.GetFiles(
            Path.Combine(solutionRoot, "src"),
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (var projectPath in productionProjects)
        {
            var document = XDocument.Load(projectPath);
            var packageNames = document.Root!
                .Elements("ItemGroup")
                .Elements("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(name => name is not null);

            Assert.DoesNotContain(
                packageNames,
                name => name!.Contains("Sqlite", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void GeneratedSqliteDatabaseIsNotPresent()
    {
        var solutionRoot = FindSolutionRoot();

        Assert.False(
            File.Exists(Path.Combine(solutionRoot, "src", "ToyStore.Web", "Data", "app.db")));
    }

    [Fact]
    public void TestProjectsDoNotReferenceXunitV2()
    {
        var solutionRoot = FindSolutionRoot();
        var testProjects = Directory.GetFiles(
            Path.Combine(solutionRoot, "tests"),
            "*.csproj",
            SearchOption.AllDirectories);

        foreach (var projectPath in testProjects)
        {
            var document = XDocument.Load(projectPath);
            var packageNames = document.Root!
                .Elements("ItemGroup")
                .Elements("PackageReference")
                .Select(reference => reference.Attribute("Include")?.Value)
                .Where(name => name is not null);

            Assert.DoesNotContain(
                packageNames,
                name => string.Equals(name, "xunit", StringComparison.OrdinalIgnoreCase));
        }
    }

    [Fact]
    public void InfrastructureDoesNotDuplicateDependencyInjectionFromAspNetCoreFramework()
    {
        var solutionRoot = FindSolutionRoot();
        var projectPath = Path.Combine(
            solutionRoot,
            "src",
            "ToyStore.Infrastructure",
            "ToyStore.Infrastructure.csproj");
        var document = XDocument.Load(projectPath);
        var packageNames = document.Root!
            .Elements("ItemGroup")
            .Elements("PackageReference")
            .Select(reference => reference.Attribute("Include")?.Value);

        Assert.DoesNotContain(
            packageNames,
            name => string.Equals(
                name,
                "Microsoft.Extensions.DependencyInjection.Abstractions",
                StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatPackages(Dictionary<string, string?> packages) =>
        packages.Count == 0
            ? "<none>"
            : string.Join(
                ", ",
                packages
                    .OrderBy(package => package.Key, StringComparer.OrdinalIgnoreCase)
                    .Select(package => $"{package.Key} {package.Value ?? "<missing version>"}"));

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
            $"Could not locate ToyStore.sln by walking upward from '{AppContext.BaseDirectory}'.");
    }
}
