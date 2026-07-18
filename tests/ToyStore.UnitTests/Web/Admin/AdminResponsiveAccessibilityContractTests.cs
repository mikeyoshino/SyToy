using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminResponsiveAccessibilityContractTests
{
    [Fact]
    public void LayoutSupportsMobileTabletCompactAndWideDesktopWithoutBodyOverflow()
    {
        var adminRoot = AdminRoot();
        var layout = File.ReadAllText(Path.Combine(adminRoot, "Layout", "AdminLayout.razor.css"));
        var rail = File.ReadAllText(Path.Combine(adminRoot, "Navigation", "AdminRail.razor.css"));
        var mobile = File.ReadAllText(Path.Combine(adminRoot, "Navigation", "AdminMobileNavigation.razor.css"));
        var context = File.ReadAllText(Path.Combine(adminRoot, "Navigation", "AdminContextNav.razor.css"));
        var table = File.ReadAllText(Path.Combine(adminRoot, "Shared", "AdminDataTable.razor.css"));
        var modal = File.ReadAllText(Path.Combine(adminRoot, "Shared", "AdminModal.razor.css"));

        Assert.Contains("overflow-x: clip", layout, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", layout, StringComparison.Ordinal);
        Assert.Matches(@"(?s)@media \(min-width: 56\.25rem\).*?grid-template-columns: 5\.25rem minmax\(0, 1fr\)", layout);
        Assert.Matches(@"(?s)@media \(min-width: 75rem\).*?grid-template-columns: 16\.5rem minmax\(0, 1fr\)", layout);
        Assert.Contains("position: sticky", rail, StringComparison.Ordinal);
        Assert.Contains("height: 100dvh", rail, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", mobile, StringComparison.Ordinal);
        Assert.Contains("display: none", mobile, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", context, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", table, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)@media \(max-width: 56\.249rem\).*?width: 100vw.*?max-width: none.*?height: 100dvh.*?max-height: none.*?border-radius: 0",
            modal);
    }

    [Fact]
    public void KeyboardFocusTargetsDrawerModesAndReducedMotionStayExplicit()
    {
        var webRoot = WebRoot();
        var adminCss = File.ReadAllText(Path.Combine(webRoot, "wwwroot", "css", "admin.css"));
        var layout = File.ReadAllText(Path.Combine(AdminRoot(), "Layout", "AdminLayout.razor"));
        var rail = File.ReadAllText(Path.Combine(AdminRoot(), "Navigation", "AdminRail.razor"));
        var mobile = File.ReadAllText(Path.Combine(AdminRoot(), "Navigation", "AdminMobileNavigation.razor"));
        var script = File.ReadAllText(Path.Combine(webRoot, "Components", "Feedback", "StoreDialog.razor.js"));
        var forms = File.ReadAllText(Path.Combine(webRoot, "wwwroot", "css", "forms.css"));

        Assert.Contains("href=\"#admin-main\"", layout, StringComparison.Ordinal);
        Assert.Contains("tabindex=\"-1\"", layout, StringComparison.Ordinal);
        Assert.Contains("title=\"@item.Label\"", rail, StringComparison.Ordinal);
        Assert.Contains("aria-pressed", rail, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", mobile, StringComparison.Ordinal);
        Assert.Contains("CloseWithoutFocusReturnAsync", mobile, StringComparison.Ordinal);
        Assert.Contains("restoreFocusOnClose", script, StringComparison.Ordinal);
        Assert.Contains("closeWithoutFocusReturn", script, StringComparison.Ordinal);
        Assert.Contains("returnFocusElement.focus()", script, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)export function close\(dialog\)\s*\{.*?if \(dialog\.open\)\s*\{\s*if \(state\).*?restoreFocusOnClose = true;.*?dialog\.close\(\);",
            script);
        Assert.Matches(
            @"(?s)export function dispose\(dialog\).*?state\.restoreFocusOnClose\s*&&\s*state\.returnFocusElement",
            script);
        Assert.Contains("prefers-reduced-motion: reduce", adminCss, StringComparison.Ordinal);
        Assert.Contains("transition-duration: .01ms !important", adminCss, StringComparison.Ordinal);
        Assert.Contains("appearance: none", forms, StringComparison.Ordinal);
        Assert.Contains("-webkit-appearance: none", forms, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("#3e515d", "#eef2f4")]
    [InlineData("#15212a", "#eaf6fb")]
    [InlineData("#176b49", "#e7f6ee")]
    [InlineData("#704600", "#fff3d6")]
    [InlineData("#913630", "#ffedeb")]
    public void EveryStatusBadgePairPassesNormalTextContrast(string foreground, string background)
    {
        Assert.True(Contrast(foreground, background) >= 4.5);
    }

    [Fact]
    public void AdminSourceAddsNoPackageRemoteAssetBase64FakeDataOrForbiddenDependency()
    {
        var webRoot = WebRoot();
        var project = File.ReadAllText(Path.Combine(webRoot, "ToyStore.Web.csproj"));
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(AdminRoot(), "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                    || path.EndsWith(".cs", StringComparison.Ordinal)
                    || path.EndsWith(".css", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        var packages = Regex.Matches(project, "PackageReference Include=\"(?<name>[^\"]+)\"")
            .Select(match => match.Groups["name"].Value)
            .ToArray();
        Assert.Equal(
            ["Microsoft.EntityFrameworkCore.Design", "Serilog.AspNetCore"],
            packages);
        Assert.DoesNotContain("data:image", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ToyStore.Domain", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"\b(?:ยอดขาย|คำสั่งซื้อ)\s*[:=]\s*[0-9]", source);
        Assert.DoesNotMatch("href\\s*=\\s*[\\\"']?/admin/categories", source);
    }

    [Fact]
    public void RealChromeSmokeArtifactIsReproducibleAuditableAndPackageFree()
    {
        var repositoryRoot = RepositoryRoot();
        var scriptPath = Path.Combine(repositoryRoot, "scripts", "admin-shell-smoke.py");
        var reportPath = Path.Combine(
            repositoryRoot,
            "artifacts",
            "browser",
            "admin-shell-smoke-report.json");

        Assert.True(File.Exists(scriptPath));
        Assert.True(File.Exists(reportPath));
        var script = File.ReadAllText(scriptPath);
        Assert.Contains("Chrome DevTools Protocol", script, StringComparison.Ordinal);
        Assert.Contains("390", script, StringComparison.Ordinal);
        Assert.Contains("768", script, StringComparison.Ordinal);
        Assert.Contains("900", script, StringComparison.Ordinal);
        Assert.Contains("1199", script, StringComparison.Ordinal);
        Assert.Contains("1200", script, StringComparison.Ordinal);
        Assert.Contains("admin-modal", script, StringComparison.Ordinal);
        Assert.Contains("focus", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("overflow", script, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("prefers-reduced-motion", script, StringComparison.Ordinal);
        Assert.DoesNotContain("import playwright", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("from selenium", script, StringComparison.OrdinalIgnoreCase);

        using var report = JsonDocument.Parse(File.ReadAllText(reportPath));
        Assert.Equal("passed", report.RootElement.GetProperty("result").GetString());
        Assert.True(report.RootElement.GetProperty("assertions").GetArrayLength() >= 20);
    }

    private static double Contrast(string first, string second)
    {
        var firstLuminance = Luminance(first);
        var secondLuminance = Luminance(second);
        return (Math.Max(firstLuminance, secondLuminance) + .05)
             / (Math.Min(firstLuminance, secondLuminance) + .05);
    }

    private static double Luminance(string color)
    {
        var channels = new[] { color[1..3], color[3..5], color[5..7] }
            .Select(value => int.Parse(value, NumberStyles.HexNumber, CultureInfo.InvariantCulture) / 255d)
            .Select(value => value <= .04045
                ? value / 12.92
                : Math.Pow((value + .055) / 1.055, 2.4))
            .ToArray();
        return .2126 * channels[0] + .7152 * channels[1] + .0722 * channels[2];
    }

    private static string AdminRoot() => Path.Combine(WebRoot(), "Components", "Admin");

    private static string WebRoot() => Path.Combine(RepositoryRoot(), "src", "ToyStore.Web");

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
