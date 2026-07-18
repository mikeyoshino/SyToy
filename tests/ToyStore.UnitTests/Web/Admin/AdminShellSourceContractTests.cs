namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminShellSourceContractTests
{
    [Fact]
    public void LayoutOwnsSkipMainNavigationLifecycleAndSharedMobileDrawer()
    {
        var webRoot = Path.Combine(RepositoryRoot(), "src", "ToyStore.Web");
        var layout = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Layout",
            "AdminLayout.razor"));
        var css = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Layout",
            "AdminLayout.razor.css"));
        var mobile = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Navigation",
            "AdminMobileNavigation.razor"));
        var context = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Navigation",
            "AdminContextNav.razor"));
        var rail = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Navigation",
            "AdminRail.razor"));

        Assert.Contains("href=\"#admin-main\"", layout, StringComparison.Ordinal);
        Assert.Contains("<main id=\"admin-main\" tabindex=\"-1\">", layout, StringComparison.Ordinal);
        Assert.Contains("LocationChanged +=", layout, StringComparison.Ordinal);
        Assert.Contains("LocationChanged -=", layout, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource", layout, StringComparison.Ordinal);
        Assert.Contains("if (disposed", layout, StringComparison.Ordinal);
        Assert.Contains("<AdminRail", layout, StringComparison.Ordinal);
        Assert.Contains("<AdminMobileNavigation", layout, StringComparison.Ordinal);
        Assert.Contains("@media (min-width: 56.25rem)", css, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns", css, StringComparison.Ordinal);
        Assert.Contains("<StoreDrawer", mobile, StringComparison.Ordinal);
        Assert.Contains("CloseWithoutFocusReturnAsync", mobile, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", mobile, StringComparison.Ordinal);
        Assert.Contains("Escape", mobile, StringComparison.Ordinal);
        Assert.Contains("aria-current", context, StringComparison.Ordinal);
        Assert.Contains("? \"page\" : \"location\"", rail, StringComparison.Ordinal);
        Assert.Contains("title=\"@item.Label\"", rail, StringComparison.Ordinal);
        Assert.Contains("title=\"ออกจากระบบ\"", rail, StringComparison.Ordinal);
        Assert.Contains("? \"page\" : \"location\"", mobile, StringComparison.Ordinal);
        var railCss = File.ReadAllText(Path.Combine(
            webRoot,
            "Components",
            "Admin",
            "Navigation",
            "AdminRail.razor.css"));
        Assert.Contains("position: sticky", railCss, StringComparison.Ordinal);
        Assert.Contains("height: 100dvh", railCss, StringComparison.Ordinal);
        Assert.Contains("overflow-y: auto", railCss, StringComparison.Ordinal);
        Assert.Contains("overflow-x: auto", File.ReadAllText(Path.ChangeExtension(
            Path.Combine(webRoot, "Components", "Admin", "Navigation", "AdminContextNav.razor"),
            ".razor.css")), StringComparison.Ordinal);
    }

    [Fact]
    public void IconsAreCentralizedAndDecorativeSvgIsHidden()
    {
        var adminRoot = Path.Combine(RepositoryRoot(), "src", "ToyStore.Web", "Components", "Admin");
        var icon = File.ReadAllText(Path.Combine(adminRoot, "Navigation", "AdminIcon.razor"));
        var otherRazor = string.Join(
            Environment.NewLine,
            Directory.GetFiles(adminRoot, "*.razor", SearchOption.AllDirectories)
                .Where(path => !path.EndsWith("AdminIcon.razor", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.Contains("<svg", icon, StringComparison.Ordinal);
        Assert.Contains("aria-hidden=\"true\"", icon, StringComparison.Ordinal);
        Assert.DoesNotContain("<svg", otherRazor, StringComparison.Ordinal);
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
