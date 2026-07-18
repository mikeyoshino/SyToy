# M1-02 Dependency Direction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Configure and continuously enforce the approved Clean Architecture project dependency direction for the Toy Store solution.

**Architecture:** Production project references will exactly match the approved graph: Application depends on Domain; Infrastructure depends on Application and Domain; Web depends on Application and Infrastructure; Domain depends on no project. Each class-library layer will expose an assembly marker, while Application and Infrastructure will expose initially no-op `IServiceCollection` registration extensions ready for later package registration. Unit tests will inspect project files for the exact graph and use reflection with a real service collection to verify the runtime seams without introducing an architecture-test package.

**Tech Stack:** .NET 10, C#, MSBuild project files, Microsoft dependency-injection abstractions, xUnit

---

### Task 1: Add failing dependency-graph tests

**Files:**
- Modify: `tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj`
- Delete: `tests/ToyStore.UnitTests/UnitTest1.cs`
- Create: `tests/ToyStore.UnitTests/Architecture/ProjectDependencyTests.cs`

- [x] **Step 1: Reference the production class libraries from the unit-test project**

```xml
<ItemGroup>
  <FrameworkReference Include="Microsoft.AspNetCore.App" />
  <ProjectReference Include="../../src/ToyStore.Application/ToyStore.Application.csproj" />
  <ProjectReference Include="../../src/ToyStore.Domain/ToyStore.Domain.csproj" />
  <ProjectReference Include="../../src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj" />
</ItemGroup>
```

- [x] **Step 2: Write a test that reads every production `.csproj` and compares its normalized `ProjectReference` set with the approved graph**

```csharp
using System.Xml.Linq;

namespace ToyStore.UnitTests.Architecture;

public sealed class ProjectDependencyTests
{
    public static TheoryData<string, string[]> ApprovedDependencies => new()
    {
        { "src/ToyStore.Domain/ToyStore.Domain.csproj", [] },
        { "src/ToyStore.Application/ToyStore.Application.csproj", ["src/ToyStore.Domain/ToyStore.Domain.csproj"] },
        { "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj", ["src/ToyStore.Application/ToyStore.Application.csproj", "src/ToyStore.Domain/ToyStore.Domain.csproj"] },
        { "src/ToyStore.Web/ToyStore.Web.csproj", ["src/ToyStore.Application/ToyStore.Application.csproj", "src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj"] },
    };

    [Theory]
    [MemberData(nameof(ApprovedDependencies))]
    public void Project_references_match_the_approved_dependency_graph(string projectPath, string[] expectedReferences)
    {
        var solutionRoot = FindSolutionRoot();
        var absoluteProjectPath = Path.Combine(solutionRoot, projectPath);
        var projectDirectory = Path.GetDirectoryName(absoluteProjectPath)!;
        var document = XDocument.Load(absoluteProjectPath);

        var actualReferences = document
            .Descendants("ProjectReference")
            .Select(reference => reference.Attribute("Include")?.Value)
            .Where(include => include is not null)
            .Select(include => Path.GetFullPath(Path.Combine(projectDirectory, include!)))
            .Select(path => Path.GetRelativePath(solutionRoot, path).Replace('\\', '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expectedReferences.Order(StringComparer.Ordinal), actualReferences);
    }

    private static string FindSolutionRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate the directory containing ToyStore.sln.");
    }
}
```

- [x] **Step 3: Run the focused test and confirm RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --no-restore --filter FullyQualifiedName~ProjectDependencyTests`

Expected: three cases fail because Application, Infrastructure, and Web do not yet contain their approved project references; Domain passes.

### Task 2: Configure the approved production references

**Files:**
- Modify: `src/ToyStore.Application/ToyStore.Application.csproj`
- Modify: `src/ToyStore.Infrastructure/ToyStore.Infrastructure.csproj`
- Modify: `src/ToyStore.Web/ToyStore.Web.csproj`

- [x] **Step 1: Add Application's Domain reference and DI abstractions package**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="../ToyStore.Domain/ToyStore.Domain.csproj" />
</ItemGroup>
```

- [x] **Step 2: Add Infrastructure's Application and Domain references plus DI abstractions**

```xml
<ItemGroup>
  <PackageReference Include="Microsoft.Extensions.DependencyInjection.Abstractions" Version="10.0.3" />
</ItemGroup>
<ItemGroup>
  <ProjectReference Include="../ToyStore.Application/ToyStore.Application.csproj" />
  <ProjectReference Include="../ToyStore.Domain/ToyStore.Domain.csproj" />
</ItemGroup>
```

- [x] **Step 3: Add Web's Application and Infrastructure references**

```xml
<ItemGroup>
  <ProjectReference Include="../ToyStore.Application/ToyStore.Application.csproj" />
  <ProjectReference Include="../ToyStore.Infrastructure/ToyStore.Infrastructure.csproj" />
</ItemGroup>
```

- [x] **Step 4: Run the focused dependency test and confirm GREEN**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter FullyQualifiedName~ProjectDependencyTests`

Expected: all four dependency-graph cases pass.

### Task 3: Add assembly markers and DI registration seams through TDD

**Files:**
- Delete: `src/ToyStore.Application/Class1.cs`
- Delete: `src/ToyStore.Domain/Class1.cs`
- Delete: `src/ToyStore.Infrastructure/Class1.cs`
- Create: `src/ToyStore.Application/AssemblyReference.cs`
- Create: `src/ToyStore.Application/DependencyInjection.cs`
- Create: `src/ToyStore.Domain/AssemblyReference.cs`
- Create: `src/ToyStore.Infrastructure/AssemblyReference.cs`
- Create: `src/ToyStore.Infrastructure/DependencyInjection.cs`
- Create: `tests/ToyStore.UnitTests/Architecture/LayerRegistrationTests.cs`

- [x] **Step 1: Write reflection tests for all assembly markers and both registration methods**

```csharp
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace ToyStore.UnitTests.Architecture;

public sealed class LayerRegistrationTests
{
    [Theory]
    [InlineData("ToyStore.Application", "ToyStore.Application.AssemblyReference")]
    [InlineData("ToyStore.Domain", "ToyStore.Domain.AssemblyReference")]
    [InlineData("ToyStore.Infrastructure", "ToyStore.Infrastructure.AssemblyReference")]
    public void Layer_exposes_an_assembly_marker(string assemblyName, string markerTypeName)
    {
        var assembly = Assembly.Load(assemblyName);

        Assert.NotNull(assembly.GetType(markerTypeName));
    }

    [Theory]
    [InlineData("ToyStore.Application", "ToyStore.Application.DependencyInjection", "AddApplication")]
    [InlineData("ToyStore.Infrastructure", "ToyStore.Infrastructure.DependencyInjection", "AddInfrastructure")]
    public void Layer_registration_returns_the_same_service_collection(
        string assemblyName,
        string dependencyInjectionTypeName,
        string methodName)
    {
        var assembly = Assembly.Load(assemblyName);
        var dependencyInjectionType = Assert.IsType<Type>(assembly.GetType(dependencyInjectionTypeName));
        var method = Assert.IsType<MethodInfo>(dependencyInjectionType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static));
        var services = new ServiceCollection();

        var result = method.Invoke(null, [services]);

        Assert.Same(services, result);
    }
}
```

- [x] **Step 2: Run the focused test and confirm RED**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --no-restore --filter FullyQualifiedName~LayerRegistrationTests`

Expected: all cases fail because the marker and dependency-injection types are absent.

- [x] **Step 3: Replace generated placeholders with assembly marker classes**

```csharp
namespace ToyStore.Application;
public static class AssemblyReference { }
```

```csharp
namespace ToyStore.Domain;
public static class AssemblyReference { }
```

```csharp
namespace ToyStore.Infrastructure;
public static class AssemblyReference { }
```

- [x] **Step 4: Add minimal Application and Infrastructure registration extensions**

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ToyStore.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
```

```csharp
using Microsoft.Extensions.DependencyInjection;

namespace ToyStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        return services;
    }
}
```

- [x] **Step 5: Run the focused test and confirm GREEN**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --filter FullyQualifiedName~LayerRegistrationTests`

Expected: all marker and registration cases pass.

### Task 4: Verify, review, and close M1-02

**Files:**
- Modify: `TASKS.md`
- Review: all files changed by Tasks 1-3

- [x] **Step 1: Restore dependencies**

Run: `dotnet restore ToyStore.sln`

Expected: exit code 0 with no restore warnings requiring action.

- [x] **Step 2: Build the solution**

Run: `dotnet build ToyStore.sln --no-restore`

Expected: exit code 0. Existing generated Identity analyzer warnings, if present, remain tracked by M1-R02 and are not expanded in this task.

- [x] **Step 3: Run focused unit architecture tests**

Run: `dotnet test tests/ToyStore.UnitTests/ToyStore.UnitTests.csproj --no-build --filter FullyQualifiedName~Architecture`

Expected: all architecture tests pass.

- [x] **Step 4: Run the full solution test suite**

Run: `dotnet test ToyStore.sln --no-build`

Expected: exit code 0 with no failed tests.

- [x] **Step 5: Request an independent code review**

Review against M1-02 and this plan, focusing on exact dependency direction, real architecture-test behavior, DI seam minimality, analyzers, and scope discipline. Fix every Critical or Important issue and re-run affected focused tests.

- [x] **Step 6: Re-run fresh completion verification after review fixes**

Run: `dotnet restore ToyStore.sln`, `dotnet build ToyStore.sln --no-restore`, and `dotnet test ToyStore.sln --no-build`.

Expected: all commands exit 0; test output reports zero failures.

- [x] **Step 7: Update task tracking only after fresh verification**

Change M1-02 to `[x]`, add a concise Verified note, set Current Focus to M1-03, and keep existing review findings assigned to their documented later tasks.

## Plan self-review

- M1-02 requirements are covered: approved references, Domain isolation, markers, DI extensions, forbidden-dependency tests, focused/full verification, review, and task tracking.
- The plan intentionally does not wire `AddApplication()` or `AddInfrastructure()` into `Program.cs`; M1-05 owns startup composition.
- The plan intentionally does not remove SQLite or move Identity persistence; M1-R01/M1-R04 assign those changes to M1-03 and M2.
- The workspace has no `.git` metadata, so the Superpowers commit and SHA-based review steps cannot be performed. Review will use the explicit changed-file set and requirements instead.

## Execution notes

- TDD RED: dependency graph reported 3 failures and 1 pass before production project references were added.
- TDD GREEN: dependency graph passed 4 of 4 after adding the approved references.
- TDD RED: all 5 marker and registration cases failed before the layer types were added.
- TDD GREEN: all 5 marker and registration cases passed after the minimal implementation.
- Review: no Critical or Important findings; the single Minor finding was fixed by asserting static marker shape, `ExtensionAttribute`, and exact `IServiceCollection` signatures.
- Final verification: focused CI build produced 0 warnings and 0 errors; architecture tests passed 9 of 9; full solution tests passed 10 of 10.
