using System.Text.RegularExpressions;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminPlaceholderPageContractTests
{
    private static readonly IReadOnlyDictionary<string, (string Route, string Title)> Pages =
        new Dictionary<string, (string, string)>
        {
            ["Dashboard.razor"] = ("/admin", "ภาพรวมร้าน"),
            ["Inventory.razor"] = ("/admin/inventory", "สินค้าคงคลัง"),
            ["Notifications.razor"] = ("/admin/notifications", "การแจ้งเตือน"),
            ["Reports.razor"] = ("/admin/reports", "รายงานยอดขาย"),
            ["Settings.razor"] = ("/admin/settings", "ตั้งค่าร้าน"),
        };

    [Fact]
    public void EveryDestinationOwnsExplicitPolicyLayoutThaiMetadataAndNeutralPlaceholder()
    {
        foreach (var (fileName, contract) in Pages)
        {
            var source = File.ReadAllText(Path.Combine(PagesRoot(), fileName));

            Assert.Contains($"@page \"{contract.Route}\"", source, StringComparison.Ordinal);
            Assert.Contains("@layout AdminLayout", source, StringComparison.Ordinal);
            Assert.Contains(
                "@attribute [Authorize(Policy = PolicyNames.CanAccessAdmin)]",
                source,
                StringComparison.Ordinal);
            Assert.Contains($"<PageTitle>{contract.Title}</PageTitle>", source, StringComparison.Ordinal);
            Assert.Contains($"<AdminPageHeader Title=\"{contract.Title}\"", source, StringComparison.Ordinal);
            Assert.Equal(1, Regex.Count(source, "<AdminPageHeader\\b"));
            Assert.Contains("<AdminPhasePlaceholder", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AdminContentState", source, StringComparison.Ordinal);
            Assert.DoesNotContain("AdminDataTable", source, StringComparison.Ordinal);
            Assert.DoesNotContain("StoreSkeleton", source, StringComparison.Ordinal);
            Assert.DoesNotContain("chart", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("metric", source, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void BrandDestinationIsAnAuthorizedProductionPageInsteadOfAPlaceholder()
    {
        var source = File.ReadAllText(Path.Combine(PagesRoot(), "Brands.razor"));

        Assert.Contains("@page \"/admin/brands\"", source, StringComparison.Ordinal);
        Assert.Contains("@layout AdminLayout", source, StringComparison.Ordinal);
        Assert.Contains(
            "@attribute [Authorize(Policy = PolicyNames.CanAccessAdmin)]",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "@attribute [Authorize(Policy = PolicyNames.CanManageProducts)]",
            source,
            StringComparison.Ordinal);
        Assert.Contains("<PageTitle>แบรนด์</PageTitle>", source, StringComparison.Ordinal);
        Assert.Contains("<AdminPageHeader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<AdminPhasePlaceholder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void UniverseDestinationIsAnAuthorizedProductionPageInsteadOfAPlaceholder()
    {
        var source = File.ReadAllText(Path.Combine(PagesRoot(), "Universes.razor"));

        Assert.Contains("@page \"/admin/universes\"", source, StringComparison.Ordinal);
        Assert.Contains("@layout AdminLayout", source, StringComparison.Ordinal);
        Assert.Contains(
            "@attribute [Authorize(Policy = PolicyNames.CanAccessAdmin)]",
            source,
            StringComparison.Ordinal);
        Assert.Contains(
            "@attribute [Authorize(Policy = PolicyNames.CanManageProducts)]",
            source,
            StringComparison.Ordinal);
        Assert.Contains("<PageTitle>จักรวาล</PageTitle>", source, StringComparison.Ordinal);
        Assert.Contains("<AdminPageHeader", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<AdminPhasePlaceholder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductDestinationIsAnAuthorizedProductionPageInsteadOfAPlaceholder()
    {
        var source = File.ReadAllText(Path.Combine(PagesRoot(), "Products.razor"));

        Assert.Contains("@page \"/admin/products\"", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Policy = PolicyNames.CanAccessAdmin)]", source, StringComparison.Ordinal);
        Assert.Contains("@attribute [Authorize(Policy = PolicyNames.CanManageProducts)]", source, StringComparison.Ordinal);
        Assert.Contains("<PageTitle>สินค้า</PageTitle>", source, StringComparison.Ordinal);
        Assert.Contains("<AdminProductList", source, StringComparison.Ordinal);
        Assert.Contains("<AdminProductEditor", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<AdminPhasePlaceholder", source, StringComparison.Ordinal);
    }

    [Fact]
    public void OrderDestinationsAreAuthorizedProductionPagesInsteadOfPlaceholders()
    {
        var list = File.ReadAllText(Path.Combine(PagesRoot(), "Orders.razor"));
        var detail = File.ReadAllText(Path.Combine(PagesRoot(), "OrderDetail.razor"));

        Assert.Contains("@page \"/admin/orders\"", list, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanManageOrders", list, StringComparison.Ordinal);
        Assert.Contains("<AdminOrderFilterBar", list, StringComparison.Ordinal);
        Assert.Contains("<AdminOrderList", list, StringComparison.Ordinal);
        Assert.DoesNotContain("<AdminPhasePlaceholder", list, StringComparison.Ordinal);
        Assert.Contains("@page \"/admin/orders/{OrderNumber}\"", detail, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanManageOrders", detail, StringComparison.Ordinal);
        Assert.Contains("ประวัติการชำระเงิน", detail, StringComparison.Ordinal);
        Assert.Contains("ที่อยู่จัดส่ง Snapshot", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OnlyCatalogAndOrdersPagesUseTheirApprovedContextNavigation()
    {
        var products = File.ReadAllText(Path.Combine(PagesRoot(), "Products.razor"));
        var brands = File.ReadAllText(Path.Combine(PagesRoot(), "Brands.razor"));
        var universes = File.ReadAllText(Path.Combine(PagesRoot(), "Universes.razor"));
        var orders = File.ReadAllText(Path.Combine(PagesRoot(), "Orders.razor"));

        Assert.Contains("CatalogContextItems", products, StringComparison.Ordinal);
        Assert.Contains("CatalogContextItems", brands, StringComparison.Ordinal);
        Assert.Contains("CatalogContextItems", universes, StringComparison.Ordinal);
        Assert.Contains("OrderContextItems", orders, StringComparison.Ordinal);

        foreach (var fileName in Pages.Keys.Except(
            ["Products.razor", "Brands.razor", "Universes.razor", "Orders.razor"]))
        {
            Assert.DoesNotContain(
                "<AdminContextNav",
                File.ReadAllText(Path.Combine(PagesRoot(), fileName)),
                StringComparison.Ordinal);
        }
    }

    [Fact]
    public void AdminHasNoCategoryRouteAffordanceOrInventedCount()
    {
        var adminRoot = Directory.GetParent(PagesRoot())!.FullName;
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(adminRoot, "*", SearchOption.AllDirectories)
                .Where(path => path.EndsWith(".razor", StringComparison.Ordinal)
                    || path.EndsWith(".cs", StringComparison.Ordinal))
                .Select(File.ReadAllText));

        Assert.DoesNotContain("/admin/categories", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotMatch(@"ActionableCount\s*[:=]\s*\d", source);
    }

    private static string PagesRoot() => Path.Combine(
        RepositoryRoot(),
        "src",
        "ToyStore.Web",
        "Components",
        "Admin",
        "Pages");

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
