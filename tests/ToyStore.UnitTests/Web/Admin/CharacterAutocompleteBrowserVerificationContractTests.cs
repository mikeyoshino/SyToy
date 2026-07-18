using System.Text.Json;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class CharacterAutocompleteBrowserVerificationContractTests
{
    [Fact]
    public void DesignSystemUsesRealAdminCharacterWrapperWithFakeCallbacksOnly()
    {
        var root = RepositoryRoot();
        var source = File.ReadAllText(Path.Combine(
            root,
            "src",
            "ToyStore.Web",
            "Components",
            "Pages",
            "DesignSystem.razor"));

        Assert.Contains("data-character-autocomplete-specimen", source, StringComparison.Ordinal);
        Assert.Contains("<AdminCharacterAutocomplete", source, StringComparison.Ordinal);
        Assert.Contains("SearchOverride=\"SearchFakeCharactersAsync\"", source, StringComparison.Ordinal);
        Assert.Contains("InlineCreateOverride=\"CreateFakeCharacterAsync\"", source, StringComparison.Ordinal);
        Assert.Contains("Task.Delay", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ISender", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void RetainedHarnessUsesPackageFreeChromeAndCoversInteractionMatrix()
    {
        var scriptPath = Path.Combine(
            RepositoryRoot(),
            "scripts",
            "character-autocomplete-browser.py");

        Assert.True(File.Exists(scriptPath));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("Chrome DevTools Protocol", script, StringComparison.Ordinal);
        Assert.Contains("/design-system", script, StringComparison.Ordinal);
        Assert.Contains("role=option", script, StringComparison.Ordinal);
        Assert.Contains("aria-activedescendant", script, StringComparison.Ordinal);
        Assert.Contains("compositionstart", script, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", script, StringComparison.Ordinal);
        Assert.Contains("390", script, StringComparison.Ordinal);
        Assert.Contains("768", script, StringComparison.Ordinal);
        Assert.Contains("1200", script, StringComparison.Ordinal);
        Assert.Contains("44", script, StringComparison.Ordinal);
        Assert.DoesNotContain("import playwright", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from selenium", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetainedReportIsPassedCompleteAndSecretFree()
    {
        var reportPath = Path.Combine(
            RepositoryRoot(),
            "artifacts",
            "browser",
            "character-autocomplete-browser-report.json");

        Assert.True(File.Exists(reportPath));
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var root = report.RootElement;

        Assert.Equal("passed", root.GetProperty("result").GetString());
        Assert.True(root.GetProperty("assertions").GetArrayLength() >= 18);
        Assert.All(root.GetProperty("cleanup").EnumerateObject(), property =>
            Assert.True(property.Value.GetBoolean(), property.Name));
        var serialized = root.GetRawText();
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users/", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("/tmp/", serialized, StringComparison.Ordinal);
    }

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
