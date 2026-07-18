using ToyStore.Web.Components.Admin.Navigation;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminNavigationTests
{
    [Fact]
    public void GlobalNavigationHasApprovedThaiDestinationsUniqueIconsAndNoCategory()
    {
        var items = AdminNavigation.Items;

        Assert.Equal(
            [
                ("ภาพรวมร้าน", "/admin", "dashboard"),
                ("แคตตาล็อก", "/admin/products", "catalog"),
                ("สินค้าคงคลัง", "/admin/inventory", "inventory"),
                ("คำสั่งซื้อ", "/admin/orders", "orders"),
                ("การแจ้งเตือน", "/admin/notifications", "notifications"),
                ("รายงานยอดขาย", "/admin/reports", "reports"),
                ("ตั้งค่าร้าน", "/admin/settings", "settings"),
            ],
            items.Select(item => (item.Label, item.Href, item.RouteGroup)));
        Assert.Equal(items.Count, items.Select(item => item.Icon).Distinct().Count());
        Assert.DoesNotContain(items, item =>
            item.Label.Contains("หมวดหมู่", StringComparison.Ordinal)
            || item.Href.Contains("categories", StringComparison.Ordinal));
        Assert.All(items, item => Assert.Null(item.ActionableCount));
    }

    [Fact]
    public void ContextNavigationUsesOnlyApprovedCatalogAndOrderDestinations()
    {
        Assert.Equal(
            ["สินค้า", "แบรนด์", "จักรวาล"],
            AdminNavigation.CatalogContextItems.Select(item => item.Label));
        Assert.Equal(
            ["/admin/products", "/admin/brands", "/admin/universes"],
            AdminNavigation.CatalogContextItems.Select(item => item.Href));
        Assert.Equal(
            ["ทั้งหมด", "สินค้าพร้อมส่ง", "พรีออเดอร์", "พร้อมจัดส่ง", "จัดส่งแล้ว", "ยกเลิกแล้ว"],
            AdminNavigation.OrderContextItems.Select(item => item.Label));
        Assert.DoesNotContain(
            AdminNavigation.CatalogContextItems.Concat(AdminNavigation.OrderContextItems),
            item => item.Label is "ภาพรวมร้าน" or "แคตตาล็อก" or "สินค้าคงคลัง"
                or "คำสั่งซื้อ" or "การแจ้งเตือน" or "รายงานยอดขาย" or "ตั้งค่าร้าน");
    }

    [Theory]
    [InlineData("dashboard", "/admin", true)]
    [InlineData("dashboard", "/admin/", true)]
    [InlineData("dashboard", "/administrator", false)]
    [InlineData("catalog", "/admin/products", true)]
    [InlineData("catalog", "/admin/products/new", true)]
    [InlineData("catalog", "https://shop.example/admin/brands?search=a", true)]
    [InlineData("catalog", "/admin/universes/", true)]
    [InlineData("orders", "/admin/orders/ABC-1", true)]
    [InlineData("orders", "/admin/orders-old", false)]
    public void RouteGroupsHandleExactNestedAbsoluteTrailingAndFalsePrefixPaths(
        string routeGroup,
        string currentUri,
        bool expected)
    {
        Assert.Equal(expected, AdminRouteMatcher.IsGroupActive(routeGroup, currentUri));
    }

    [Theory]
    [InlineData("/admin/products", "/admin/products", true)]
    [InlineData("/admin/products", "/admin/products/42", false)]
    [InlineData("/admin/products", "/admin/brands", false)]
    [InlineData("/admin/orders", "https://shop.example/admin/orders?type=pre-order", true)]
    public void ExactPathSeparatesCurrentPageFromCurrentGroupedLocation(
        string destination,
        string currentUri,
        bool expected)
    {
        Assert.Equal(expected, AdminRouteMatcher.IsExactPath(destination, currentUri));
    }

    [Theory]
    [InlineData("/admin/orders", "/admin/orders", true)]
    [InlineData("/admin/orders", "/admin/orders?search=SY-001&page=2", true)]
    [InlineData("/admin/orders", "/admin/orders?type=pre-order", false)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?type=pre-order", true)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?search=robot&type=pre-order&page=2", true)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?type=in-stock", false)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?type=pre-order&status=shipped", false)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?type=pre-order&type=pre-order", false)]
    [InlineData("/admin/orders?type=pre-order", "/admin/orders?type=pre-order&type=in-stock", false)]
    [InlineData("/admin/orders?status=ready-to-ship", "https://shop.example/admin/orders?status=ready-to-ship", true)]
    [InlineData("/admin/orders?status=ready-to-ship", "/admin/orders?status=ready-to-ship&status=shipped", false)]
    public void ContextMatchingPreservesQuerySensitiveOrderUrls(
        string destination,
        string currentUri,
        bool expected)
    {
        Assert.Equal(expected, AdminRouteMatcher.IsContextActive(destination, currentUri));
    }

    [Theory]
    [InlineData("/admin/orders")]
    [InlineData("/admin/orders?search=SY-001")]
    [InlineData("/admin/orders?type=pre-order")]
    [InlineData("/admin/orders?type=pre-order&search=robot")]
    [InlineData("/admin/orders?type=pre-order&status=ready-to-ship")]
    [InlineData("/admin/orders?type=pre-order&type=pre-order")]
    [InlineData("/admin/orders?type=pre-order&type=in-stock")]
    [InlineData("/admin/orders?status=unknown")]
    public void OrderContextSelectsAtMostOneApprovedShortcut(string currentUri)
    {
        var active = AdminNavigation.OrderContextItems.Count(item =>
            AdminRouteMatcher.IsContextActive(item.Href, currentUri));

        Assert.InRange(active, 0, 1);
    }
}
