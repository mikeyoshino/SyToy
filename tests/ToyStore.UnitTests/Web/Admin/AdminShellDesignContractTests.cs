using System.Globalization;
using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminShellDesignContractTests
{
    [Fact]
    public void AdminThemeLocksExactMutedOceanTokensAndSharedMotion()
    {
        var tokens = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "tokens.css"));
        var admin = File.ReadAllText(Path.Combine(WebRoot(), "wwwroot", "css", "admin.css"));

        AssertToken(tokens, "admin-accent", "#3f91b8");
        AssertToken(tokens, "admin-accent-strong", "#2f789b");
        AssertToken(tokens, "admin-accent-soft", "#eaf6fb");
        AssertToken(tokens, "admin-bg", "#f5f8fa");
        AssertToken(tokens, "admin-surface", "#ffffff");
        AssertToken(tokens, "admin-ink", "#15212a");
        AssertToken(tokens, "admin-muted", "#667782");
        AssertToken(tokens, "font-family-base", "\"Noto Sans Thai\", system-ui, -apple-system, sans-serif");
        AssertToken(tokens, "duration-fast", "160ms");
        AssertToken(tokens, "duration-normal", "260ms");

        Assert.Contains(".admin-shell", admin, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", admin, StringComparison.Ordinal);
        Assert.Contains("min-width: 2.75rem", admin, StringComparison.Ordinal);
        Assert.Matches(
            @"(?s)@media\s*\(prefers-reduced-motion:\s*reduce\).*?transition-duration:\s*\.01ms\s*!important",
            admin);
        Assert.DoesNotContain("#dfff29", admin, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("var(--color-accent)", admin, StringComparison.Ordinal);
    }

    [Fact]
    public void ApprovedAdminPairingsMeetComputedWcagContrastAndRejectWhiteOnBaseAccent()
    {
        AssertContrast("#15212a", "#f5f8fa", 4.5);
        AssertContrast("#15212a", "#ffffff", 4.5);
        AssertContrast("#667782", "#ffffff", 4.5);
        AssertContrast("#ffffff", "#2f789b", 4.5);
        AssertContrast("#15212a", "#3f91b8", 4.5);
        AssertContrast("#15212a", "#eaf6fb", 4.5);
        AssertContrast("#2f789b", "#f5f8fa", 3.0);
        Assert.True(Contrast("#ffffff", "#3f91b8") < 4.5);
    }

    [Fact]
    public void AdminThemeLoadsAfterSharedControlsWithoutAssetsOrDuplicateControlFamilies()
    {
        var webRoot = WebRoot();
        var app = File.ReadAllText(Path.Combine(webRoot, "Components", "App.razor"));
        var forms = app.IndexOf("css/forms.css", StringComparison.Ordinal);
        var feedback = app.IndexOf("css/feedback.css", StringComparison.Ordinal);
        var admin = app.IndexOf("css/admin.css", StringComparison.Ordinal);
        var scoped = app.IndexOf("ToyStore.Web.styles.css", StringComparison.Ordinal);

        Assert.True(forms >= 0 && forms < feedback && feedback < admin && admin < scoped);
        var adminRoot = Path.Combine(webRoot, "Components", "Admin");
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(adminRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                    || path.EndsWith(".cs", StringComparison.Ordinal)
                    || path.EndsWith(".css", StringComparison.Ordinal))
                .Select(File.ReadAllText));
        Assert.DoesNotContain("data:image", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("http://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("https://", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"border:\s*[2-9]px\s+solid", source);
        Assert.Contains("<StoreDrawer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminTextField", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminNumberField", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminSelectField", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminDrawer", source, StringComparison.Ordinal);
        Assert.DoesNotContain("AdminDialog", source, StringComparison.Ordinal);
    }

    private static void AssertToken(string css, string name, string value) =>
        Assert.Matches(
            $@"--{Regex.Escape(name)}:\s*{Regex.Escape(value)}\s*;",
            css);

    private static void AssertContrast(string foreground, string background, double minimum) =>
        Assert.True(
            Contrast(foreground, background) >= minimum,
            $"{foreground} on {background} must meet {minimum.ToString(CultureInfo.InvariantCulture)}:1.");

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
