namespace ToyStore.Web.Components.Admin.Navigation;

public static class AdminNavigation
{
    public static IReadOnlyList<AdminNavigationItem> Items { get; } =
    [
        new("ภาพรวมร้าน", "/admin", AdminIconName.Dashboard, "dashboard"),
        new("แคตตาล็อก", "/admin/products", AdminIconName.Catalog, "catalog"),
        new("สินค้าคงคลัง", "/admin/inventory", AdminIconName.Inventory, "inventory"),
        new("คำสั่งซื้อ", "/admin/orders", AdminIconName.Orders, "orders"),
        new("การแจ้งเตือน", "/admin/notifications", AdminIconName.Notifications, "notifications"),
        new("รายงานยอดขาย", "/admin/reports", AdminIconName.Reports, "reports"),
        new("ตั้งค่าร้าน", "/admin/settings", AdminIconName.Settings, "settings"),
    ];

    public static IReadOnlyList<AdminContextNavigationItem> CatalogContextItems { get; } =
    [
        new("สินค้า", "/admin/products"),
        new("แบรนด์", "/admin/brands"),
        new("จักรวาล", "/admin/universes"),
    ];

    public static IReadOnlyList<AdminContextNavigationItem> OrderContextItems { get; } =
    [
        new("ทั้งหมด", "/admin/orders"),
        new("สินค้าพร้อมส่ง", "/admin/orders?type=in-stock"),
        new("พรีออเดอร์", "/admin/orders?type=pre-order"),
        new("พร้อมจัดส่ง", "/admin/orders?status=ready-to-ship"),
        new("จัดส่งแล้ว", "/admin/orders?status=shipped"),
        new("ยกเลิกแล้ว", "/admin/orders?status=cancelled"),
    ];
}
