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
        Assert.Contains("InitialFocusSelector=\".store-dialog__close\"", source, StringComparison.Ordinal);
        Assert.Contains("ยอดสินค้า", source, StringComparison.Ordinal);
        Assert.Contains("คำนวณในขั้นตอนถัดไป", source, StringComparison.Ordinal);
        Assert.Contains("ชำระเงินอย่างปลอดภัย", source, StringComparison.Ordinal);
        Assert.Contains("ช้อปปิ้งต่อ", source, StringComparison.Ordinal);
        Assert.Contains("StoreButtonTone.Ghost", source, StringComparison.Ordinal);
        Assert.Contains("FullWidth=\"true\"", source, StringComparison.Ordinal);
        Assert.Contains("addedItem?.BrandSlug", source, StringComparison.Ordinal);
        Assert.Contains("NavigationManager.NavigateTo(continueShoppingUrl)", source, StringComparison.Ordinal);
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
        var feedback = Read("src", "ToyStore.Web", "wwwroot", "css", "feedback.css");

        Assert.Contains("@media (max-width: 35rem)", source, StringComparison.Ordinal);
        Assert.Contains("transition", source, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion: reduce", source, StringComparison.Ordinal);
        Assert.Contains("min-height: 2.75rem", source, StringComparison.Ordinal);
        Assert.Contains("position: fixed", feedback, StringComparison.Ordinal);
        Assert.Contains("inset-inline: auto 0", feedback, StringComparison.Ordinal);
        Assert.Contains("width: 100%", feedback, StringComparison.Ordinal);
        Assert.Contains(".store-cart:focus { outline: none; }", source, StringComparison.Ordinal);
        Assert.Contains("env(safe-area-inset-bottom)", source, StringComparison.Ordinal);
        Assert.Contains("border-radius: var(--radius-pill)", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".store-cart__continue-action { display: none; }", source, StringComparison.Ordinal);
        Assert.Contains("justify-content: flex-end", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedDrawerAnimationCannotRemainFrozenOffCanvasOnMobileWebKit()
    {
        var script = Read("src", "ToyStore.Web", "Components", "Feedback", "StoreDialog.razor.js");
        var feedback = Read("src", "ToyStore.Web", "wwwroot", "css", "feedback.css");

        Assert.Contains("Promise.race([animation.finished, timeout])", script, StringComparison.Ordinal);
        Assert.Contains("fill: \"none\"", script, StringComparison.Ordinal);
        Assert.Contains("surface.style.removeProperty(\"transform\")", script, StringComparison.Ordinal);
        Assert.Contains("usesFullscreenDrawer(dialog)", script, StringComparison.Ordinal);
        Assert.Contains("transform: none !important", feedback, StringComparison.Ordinal);
        Assert.Contains("min-width: 100%", feedback, StringComparison.Ordinal);
        Assert.DoesNotContain("transition: transform", feedback, StringComparison.Ordinal);
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
