namespace ToyStore.UnitTests.Web;

public sealed class CartDrawerSourceContractTests
{
    [Fact]
    public void DrawerUsesAuthoritativeSenderBrowserIdentityOnlyAndAccessibleStates()
    {
        var source = Read("src", "ToyStore.Web", "Components", "Cart", "StoreCartDrawer.razor");

        Assert.Contains("ISender", source, StringComparison.Ordinal);
        Assert.Contains("GetCartQuery", source, StringComparison.Ordinal);
        Assert.Contains("GetAnonymousCartPreviewQuery", source, StringComparison.Ordinal);
        Assert.Contains("ChangeCartItemQuantityCommand", source, StringComparison.Ordinal);
        Assert.Contains("RemoveCartItemCommand", source, StringComparison.Ordinal);
        Assert.Contains("MergeAnonymousCartCommand", source, StringComparison.Ordinal);
        Assert.Contains("RejectedItems", source, StringComparison.Ordinal);
        Assert.Contains("ClampedItems", source, StringComparison.Ordinal);
        Assert.Contains("SemaphoreSlim", source, StringComparison.Ordinal);
        Assert.Contains("EnsureBrowserReadyAsync", source, StringComparison.Ordinal);
        Assert.Contains("CanAddDistinctProduct", source, StringComparison.Ordinal);
        Assert.Contains("สูงสุด 100 รายการ", source, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", source, StringComparison.Ordinal);
        Assert.Contains("role=\"alert\"", source, StringComparison.Ordinal);
        Assert.Contains("ตะกร้ายังว่างอยู่", source, StringComparison.Ordinal);
        Assert.Contains("ตรวจสอบราคาและสต็อกอีกครั้ง", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DbContext", source, StringComparison.Ordinal);
    }

    [Fact]
    public void BrowserStorePersistsOnlyMergeIdentityProductAndQuantity()
    {
        var source = Read("src", "ToyStore.Web", "Components", "Cart", "StoreCartDrawer.razor.js");

        Assert.Contains("localStorage", source, StringComparison.Ordinal);
        Assert.Contains("Array.isArray", source, StringComparison.Ordinal);
        Assert.Contains("Number.isInteger", source, StringComparison.Ordinal);
        Assert.Contains("mergeOperationId", source, StringComparison.Ordinal);
        Assert.Contains("productId", source, StringComparison.Ordinal);
        Assert.Contains("quantity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("price", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stock", source, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("name", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DrawerStylesAreResponsiveAnimatedAndRespectReducedMotion()
    {
        var source = Read("src", "ToyStore.Web", "Components", "Cart", "StoreCartDrawer.razor.css");

        Assert.Contains("@media (max-width: 35rem)", source, StringComparison.Ordinal);
        Assert.Contains("transition", source, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", source, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", source, StringComparison.Ordinal);
    }

    private static string Read(params string[] path) =>
        File.ReadAllText(Path.Combine([Root(), .. path]));

    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
