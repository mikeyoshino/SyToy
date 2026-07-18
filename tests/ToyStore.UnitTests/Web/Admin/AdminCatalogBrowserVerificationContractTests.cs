using System.Text.Json;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminCatalogBrowserVerificationContractTests
{
    [Fact]
    public void RetainedBrowserHarnessOwnsASecretFreeDisposableEnvironment()
    {
        var root = RepositoryRoot();
        var scriptPath = Path.Combine(root, "scripts", "admin-catalog-browser.py");

        Assert.True(File.Exists(scriptPath));
        var script = File.ReadAllText(scriptPath);

        Assert.Contains("Chrome DevTools Protocol", script, StringComparison.Ordinal);
        Assert.Contains("tempfile.TemporaryDirectory", script, StringComparison.Ordinal);
        Assert.Contains("--bootstrap-admin", script, StringComparison.Ordinal);
        Assert.Contains("CREATE DATABASE", script, StringComparison.Ordinal);
        Assert.Contains("DROP DATABASE", script, StringComparison.Ordinal);
        Assert.Contains("/health/ready", script, StringComparison.Ordinal);
        Assert.Contains("/admin/brands", script, StringComparison.Ordinal);
        Assert.Contains("/admin/universes", script, StringComparison.Ordinal);
        Assert.Contains("390", script, StringComparison.Ordinal);
        Assert.Contains("768", script, StringComparison.Ordinal);
        Assert.Contains("900", script, StringComparison.Ordinal);
        Assert.Contains("1199", script, StringComparison.Ordinal);
        Assert.Contains("1200", script, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", script, StringComparison.Ordinal);
        Assert.Contains("DOM.setFileInputFiles", script, StringComparison.Ordinal);
        Assert.Contains("PostgresCommitFaultProxy", script, StringComparison.Ordinal);
        Assert.Contains("SSL Mode=Disable", script, StringComparison.Ordinal);
        Assert.Contains("commit-acknowledgement loss", script, StringComparison.Ordinal);
        Assert.DoesNotContain("import playwright", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from selenium", script, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void RetainedReportContainsOnlyAuditableSafeEvidence()
    {
        var root = RepositoryRoot();
        var reportPath = Path.Combine(
            root,
            "artifacts",
            "browser",
            "admin-catalog-browser-report.json");

        Assert.True(File.Exists(reportPath));
        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        var rootElement = report.RootElement;

        Assert.Equal("passed", rootElement.GetProperty("result").GetString());
        Assert.True(rootElement.GetProperty("assertions").GetArrayLength() >= 30);
        Assert.True(rootElement.GetProperty("cleanup").GetProperty("applicationStopped").GetBoolean());
        Assert.True(rootElement.GetProperty("cleanup").GetProperty("chromeStopped").GetBoolean());
        Assert.True(rootElement.GetProperty("cleanup").GetProperty("databaseDropped").GetBoolean());
        Assert.True(rootElement.GetProperty("cleanup").GetProperty("temporaryFilesRemoved").GetBoolean());
        Assert.True(rootElement.GetProperty("cleanup").GetProperty("proxyStopped").GetBoolean());

        var serialized = rootElement.GetRawText();
        Assert.DoesNotContain("password", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookie", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("/Users/", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("/tmp/", serialized, StringComparison.Ordinal);
        Assert.DoesNotContain("filename", serialized, StringComparison.OrdinalIgnoreCase);
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new DirectoryNotFoundException("Could not find the repository root.");
    }
}
