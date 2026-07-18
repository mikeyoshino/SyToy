using System.Globalization;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Web.Components.Storefront.Models;

namespace ToyStore.UnitTests.Web;

public sealed class StorefrontCatalogPageContractTests
{
    [Fact]
    public void CatalogOwnsAllRoutesCanonicalUrlFiltersPagingAndRaceSafeLoading()
    {
        var source = WebSource("Components/Pages/Catalog.razor");
        Assert.Contains("@page \"/products\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/search\"", source, StringComparison.Ordinal);
        Assert.Contains("@page \"/brands/{BrandSlug}\"", source, StringComparison.Ordinal);
        foreach (var query in new[] { "q", "type", "category", "brand", "character", "universe", "min", "max", "page" })
            Assert.Contains($"[SupplyParameterFromQuery(Name = \"{query}\")]", source, StringComparison.Ordinal);
        Assert.Contains("StorefrontPagination", source, StringComparison.Ordinal);
        Assert.Contains("loadGeneration", source, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource? loadTokenSource", source, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\"", source, StringComparison.Ordinal);
        Assert.Contains("rel=\"canonical\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-expanded=\"@filtersExpanded\"", source, StringComparison.Ordinal);
        Assert.Contains("aria-controls=\"catalog-filter-panel\"", source, StringComparison.Ordinal);
        Assert.Contains("ActiveFilterCount", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
    }

    [Fact]
    public void FiltersReuseSharedCrossBrowserFieldsAndDetailOwnsSemanticSwipeGalleryAndSeo()
    {
        var filters = WebSource("Components/Storefront/StorefrontCatalogFilterBar.razor");
        var detail = WebSource("Components/Pages/ProductDetail.razor");
        var gallery = WebSource("Components/Storefront/StorefrontProductGallery.razor");
        var galleryCss = WebSource("Components/Storefront/StorefrontProductGallery.razor.css");
        Assert.Contains("StoreSelectField", filters, StringComparison.Ordinal);
        Assert.Contains("StoreNumberField", filters, StringComparison.Ordinal);
        Assert.DoesNotMatch("(?i)<select(?:\\s|>)", filters);
        Assert.Contains("<article", detail, StringComparison.Ordinal);
        Assert.Contains("<dl", detail, StringComparison.Ordinal);
        Assert.Contains("og:title", detail, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource? loadTokenSource", detail, StringComparison.Ordinal);
        Assert.Contains("NavigationManager.ToAbsoluteUri", detail, StringComparison.Ordinal);
        Assert.Contains("NavigationManager.NotFound()", detail, StringComparison.Ordinal);
        Assert.Contains("<StoreButton OnClick=\"LoadAsync\"", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("<main", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"รูปภาพสินค้า\"", gallery, StringComparison.Ordinal);
        Assert.Contains("scroll-snap-type: x mandatory", galleryCss, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", galleryCss, StringComparison.Ordinal);
        Assert.Contains("new(\"pre-order\", \"พรีออเดอร์\")", filters, StringComparison.Ordinal);
        Assert.DoesNotContain("พรีออเดอร์ (เร็ว ๆ นี้)", filters, StringComparison.Ordinal);
    }

    [Fact]
    public void PreOrderDetailChecksEligibilityThenNavigatesToDurableCheckoutWithoutDirectReservation()
    {
        var detail = WebSource("Components/Pages/ProductDetail.razor");
        Assert.Contains("AuthenticationStateProvider", detail, StringComparison.Ordinal);
        Assert.Contains("GetPreOrderCheckoutEligibilityQuery", detail, StringComparison.Ordinal);
        Assert.Contains("StoreDialog", detail, StringComparison.Ordinal);
        Assert.Contains("StoreNumberField", detail, StringComparison.Ordinal);
        Assert.Contains("preorder=1&quantity=", detail, StringComparison.Ordinal);
        Assert.Contains("LocalReturnUrl.Normalize", detail, StringComparison.Ordinal);
        Assert.Contains("ยังไม่มีการกันสินค้าและยังไม่เกิดคำสั่งซื้อ", detail, StringComparison.Ordinal);
        Assert.Contains("มัดจำไม่คืน", detail, StringComparison.Ordinal);
        Assert.Contains("Asia/Bangkok", detail, StringComparison.Ordinal);
        Assert.Contains("ราคาเต็มต่อชิ้น", detail, StringComparison.Ordinal);
        Assert.Contains("eligibility.CloseAtUtc", detail, StringComparison.Ordinal);
        Assert.Contains("eligibility.EstimatedArrivalMonth", detail, StringComparison.Ordinal);
        Assert.Contains("eligibility.MaxPerCustomer", detail, StringComparison.Ordinal);
        Assert.Contains("eligibility.BalancePaymentDays", detail, StringComparison.Ordinal);
        Assert.Contains("ประมาณเดือน", detail, StringComparison.Ordinal);
        Assert.Contains("else if (!canSubmitPreOrder)", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("ReservePreOrderCapacityCommand", detail, StringComparison.Ordinal);
        Assert.Contains("NavigateTo($\"/checkout/preorder/{eligibility.ProductId}?quantity={eligibility.RequestedQuantity}\")", detail, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductCardOnlyCallsCartForTypedInStockProducts()
    {
        var source = WebSource("Components/Storefront/ProductCard.razor");
        var model = WebSource("Components/Storefront/Models/ProductCardModel.cs");
        Assert.Contains("StorefrontSaleType SaleType", model, StringComparison.Ordinal);
        Assert.Contains("StorefrontOfferState OfferState", model, StringComparison.Ordinal);
        Assert.Contains("Model.SaleType != StorefrontSaleType.InStock", source, StringComparison.Ordinal);
        Assert.Contains("ดูรายละเอียดพรีออเดอร์", source, StringComparison.Ordinal);
        Assert.Contains("Model.SaleType == StorefrontSaleType.InStock", source, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedProductCardMapperKeepsPreOrderPriceAndActionTypeConsistent()
    {
        var card = new StorefrontProductCard(
            Guid.NewGuid(), "สินค้าทดสอบ", "test-product", "แบรนด์", "อาร์ตทอย",
            StorefrontSaleType.PreOrder, StorefrontOfferState.PreOrderOpen, 2500, 500, 4,
            "/media/test.webp", "รูปสินค้า");

        var mapped = ProductCardModel.From(card, CultureInfo.GetCultureInfo("th-TH"));

        Assert.Equal(StorefrontSaleType.PreOrder, mapped.SaleType);
        Assert.Equal(StorefrontOfferState.PreOrderOpen, mapped.OfferState);
        Assert.Contains("มัดจำ", mapped.PriceLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("ราคาเต็ม", mapped.PriceLabel, StringComparison.Ordinal);
        Assert.Equal("พรีออเดอร์", mapped.TypeLabel);
        Assert.Equal("/products/test-product", mapped.ProductUrl);
    }

    [Fact]
    public void BrandDirectoryCompletesTheHeaderRouteWithPublishedCatalogData()
    {
        var source = WebSource("Components/Pages/BrandDirectory.razor");
        Assert.Contains("@page \"/brands\"", source, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontProductsQuery", source, StringComparison.Ordinal);
        Assert.Contains("result.Value.Brands", source, StringComparison.Ordinal);
        Assert.Contains("/brands/{brand.Slug}", source, StringComparison.Ordinal);
        Assert.Contains("<meta name=\"description\"", source, StringComparison.Ordinal);
        Assert.Contains("<StoreButton OnClick=\"LoadAsync\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("<main", source, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeUsesPublishedCatalogQueryInsteadOfInventedProductFixtures()
    {
        var source = WebSource("Components/Pages/Home.razor");
        Assert.Contains("ListStorefrontProductsQuery", source, StringComparison.Ordinal);
        Assert.Contains("StorefrontSaleTypeFilter.All", source, StringComparison.Ordinal);
        Assert.Contains("OnRetry=\"LoadFeaturedAsync\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionUrl=\"/categories\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/journal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ลาบูบู้ นักผจญภัย", source, StringComparison.Ordinal);
        Assert.Contains("ProductCardModel.From", source, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductCardKeepsMobileActionTextReadable()
    {
        var source = WebSource("Components/Storefront/ProductCard.razor.css");
        Assert.Contains(".product-card__action { font-size: .875rem; }", source, StringComparison.Ordinal);
        Assert.DoesNotContain(".product-card__action { font-size: .8125rem; }", source, StringComparison.Ordinal);
    }

    private static string WebSource(string path) => File.ReadAllText(Path.Combine(Root(), "src", "ToyStore.Web", path));
    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
