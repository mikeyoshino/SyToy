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
        Assert.Contains("requested.MaximumPrice, requested.Page, 8", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ToyStore.Infrastructure", source, StringComparison.Ordinal);
    }

    [Fact]
    public void MobileCatalogProvidesDiscoveryFilterAndRealGridSingleViewControls()
    {
        var catalog = WebSource("Components/Pages/Catalog.razor");
        var catalogStyles = WebSource("Components/Pages/Catalog.razor.css");
        var gallery = WebSource("Components/Storefront/ProductGallery.razor");
        var galleryStyles = WebSource("Components/Storefront/ProductGallery.razor.css");
        var card = WebSource("Components/Storefront/ProductCard.razor");

        Assert.Contains("catalog-page__search", catalog, StringComparison.Ordinal);
        Assert.Contains("กำลังมองหาสินค้าอะไร?", catalog, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"เมนูร้านค้า\"", catalog, StringComparison.Ordinal);
        Assert.Contains("เปิดพรีออเดอร์", catalog, StringComparison.Ordinal);
        Assert.Contains("สินค้าทั้งหมด", catalog, StringComparison.Ordinal);
        Assert.Contains("แบรนด์ทั้งหมด", catalog, StringComparison.Ordinal);
        Assert.Contains("คำสั่งซื้อของฉัน", catalog, StringComparison.Ordinal);
        Assert.Contains("เข้าสู่ระบบหรือสมัครสมาชิก", catalog, StringComparison.Ordinal);
        Assert.Contains("PolicyNames.CanViewCustomerOrders", catalog, StringComparison.Ordinal);
        Assert.Contains("catalog-page__menu-links", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("catalog-page__view-switch", catalog, StringComparison.Ordinal);
        Assert.Contains("SetDisplayMode(\"grid\")", catalog, StringComparison.Ordinal);
        Assert.Contains("SetDisplayMode(\"single\")", catalog, StringComparison.Ordinal);
        Assert.Contains("ViewMode=\"@displayMode\"", catalog, StringComparison.Ordinal);
        Assert.Contains("aria-pressed", catalog, StringComparison.Ordinal);
        Assert.Contains("position: sticky", catalogStyles, StringComparison.Ordinal);
        Assert.Contains("ViewMode", gallery, StringComparison.Ordinal);
        Assert.Contains("product-gallery--single", gallery, StringComparison.Ordinal);
        Assert.Contains("grid-template-columns: repeat(2, minmax(0, 1fr))", galleryStyles, StringComparison.Ordinal);
        Assert.Contains("SingleColumn", card, StringComparison.Ordinal);
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
        Assert.Contains("StorefrontSeoMetadata", detail, StringComparison.Ordinal);
        Assert.Contains("StorefrontStructuredData.BuildProduct", detail, StringComparison.Ordinal);
        Assert.Contains("CancellationTokenSource? loadTokenSource", detail, StringComparison.Ordinal);
        Assert.Contains("NavigationManager.ToAbsoluteUri", detail, StringComparison.Ordinal);
        Assert.Contains("NavigationManager.NotFound()", detail, StringComparison.Ordinal);
        Assert.Contains("<StoreButton OnClick=\"LoadAsync\"", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("<main", detail, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("aria-label=\"รูปภาพสินค้า\"", gallery, StringComparison.Ordinal);
        Assert.Contains("aspect-ratio: 4 / 5", galleryCss, StringComparison.Ordinal);
        Assert.Contains("store-product-gallery__thumb-button::before", galleryCss, StringComparison.Ordinal);
        Assert.Contains("object-fit: contain", galleryCss, StringComparison.Ordinal);
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
        Assert.DoesNotContain("Title=\"นโยบายมัดจำ\"", detail, StringComparison.Ordinal);
        Assert.DoesNotContain("Message=\"มัดจำไม่คืนเมื่อลูกค้ายกเลิกหรือไม่ชำระยอดคงเหลือภายในเวลาที่กำหนด\"", detail, StringComparison.Ordinal);
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
    public void ProductDetailDescriptionUsesAccessibleThreeLineExpandableTextWithFade()
    {
        var detail = WebSource("Components/Pages/ProductDetail.razor");
        var component = WebSource("Components/Feedback/StoreExpandableText.razor");
        var styles = WebSource("Components/Feedback/StoreExpandableText.razor.css");
        var script = WebSource("Components/Feedback/StoreExpandableText.razor.js");

        Assert.Contains("<StoreExpandableText Text=\"@product.Description\" />", detail, StringComparison.Ordinal);
        Assert.Contains("product-detail__benefits", detail, StringComparison.Ordinal);
        Assert.Contains("product-detail__disclosure", detail, StringComparison.Ordinal);
        Assert.Contains("รับประกันสินค้าเสียหายหรือชำรุด (แนบวิดีโอตอนเปิดกล่อง)", detail, StringComparison.Ordinal);
        Assert.Contains("ติดตามการจัดส่งได้หลังซื้อ", detail, StringComparison.Ordinal);
        Assert.Contains("ชำระเงินผ่าน Stripe อย่างปลอดภัย", detail, StringComparison.Ordinal);
        Assert.Contains("อ่านเพิ่มเติม", component, StringComparison.Ordinal);
        Assert.Contains("ย่อรายละเอียด", component, StringComparison.Ordinal);
        Assert.Contains("aria-expanded", component, StringComparison.Ordinal);
        Assert.Contains("-webkit-line-clamp: 3", styles, StringComparison.Ordinal);
        Assert.Contains("var(--color-surface) 50%", styles, StringComparison.Ordinal);
        Assert.Contains("ResizeObserver", script, StringComparison.Ordinal);
        Assert.Contains("scrollHeight > content.clientHeight", script, StringComparison.Ordinal);
    }

    [Fact]
    public void ProductDetailStickyPurchasePanelClearsTheStickyStoreHeader()
    {
        var tokens = WebSource("wwwroot/css/tokens.css");
        var headerStyles = WebSource("Components/Layout/StoreHeader.razor.css");
        var detailStyles = WebSource("Components/Pages/ProductDetail.razor.css");

        Assert.Contains("--store-header-height: 4.625rem", tokens, StringComparison.Ordinal);
        Assert.Contains("--store-header-height: 5rem", tokens, StringComparison.Ordinal);
        Assert.Contains("min-height: var(--store-header-height)", headerStyles, StringComparison.Ordinal);
        Assert.Contains("top: calc(var(--store-header-height) + var(--space-4))", detailStyles, StringComparison.Ordinal);
        Assert.Matches(@"(?s)\.product-detail__content\s*\{[^}]*position:\s*static;", detailStyles);
        Assert.Contains("grid-template-areas: \"media content\" \"additional content\"", detailStyles, StringComparison.Ordinal);
        Assert.Contains("grid-template-areas: \"media\" \"content\" \"additional\"", detailStyles, StringComparison.Ordinal);
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
    public void ProductCardProvidesAccessibleListGalleryWithoutNestedInteractiveControls()
    {
        var source = WebSource("Components/Storefront/ProductCard.razor");
        var styles = WebSource("Components/Storefront/ProductCard.razor.css");
        var script = WebSource("Components/Storefront/ProductCard.razor.js");
        var model = WebSource("Components/Storefront/Models/ProductCardModel.cs");

        Assert.Contains("Model.GalleryImages", source, StringComparison.Ordinal);
        Assert.Contains("PreviousImage", source, StringComparison.Ordinal);
        Assert.Contains("NextImage", source, StringComparison.Ordinal);
        Assert.Contains("ดูรูปก่อนหน้าของ", source, StringComparison.Ordinal);
        Assert.Contains("ดูรูปถัดไปของ", source, StringComparison.Ordinal);
        Assert.Contains("aria-live=\"polite\"", source, StringComparison.Ordinal);
        Assert.Contains("product-card__media-link", source, StringComparison.Ordinal);
        Assert.Contains("ProductCardImageModel", model, StringComparison.Ordinal);
        Assert.Contains("product-card__gallery-button", styles, StringComparison.Ordinal);
        Assert.Contains("touch-action: pan-y", styles, StringComparison.Ordinal);
        Assert.Contains("prefers-reduced-motion", styles, StringComparison.Ordinal);
        Assert.Contains("ShowPreviousImageFromSwipeAsync", source, StringComparison.Ordinal);
        Assert.Contains("ShowNextImageFromSwipeAsync", source, StringComparison.Ordinal);
        Assert.Contains("pointerdown", script, StringComparison.Ordinal);
        Assert.Contains("pointerup", script, StringComparison.Ordinal);
        Assert.Contains("minimumSwipeDistance = 40", script, StringComparison.Ordinal);
        Assert.Contains("event.preventDefault()", script, StringComparison.Ordinal);
        Assert.Contains("event.stopImmediatePropagation()", script, StringComparison.Ordinal);
    }

    [Fact]
    public void SharedProductCardMapperKeepsPreOrderPriceAndActionTypeConsistent()
    {
        var card = new StorefrontProductCard(
            Guid.NewGuid(), "สินค้าทดสอบ", "test-product", "แบรนด์", "อาร์ตทอย",
            StorefrontSaleType.PreOrder, StorefrontOfferState.PreOrderOpen, 2500, 500, 4,
            "/media/test.webp", "รูปสินค้า", Images:
            [
                new StorefrontProductImage("/media/test.webp", "รูปแรก", 0, true),
                new StorefrontProductImage("/media/test-2.webp", "รูปที่สอง", 1, false),
            ]);

        var mapped = ProductCardModel.From(card, CultureInfo.GetCultureInfo("th-TH"));

        Assert.Equal(StorefrontSaleType.PreOrder, mapped.SaleType);
        Assert.Equal(StorefrontOfferState.PreOrderOpen, mapped.OfferState);
        Assert.Contains("มัดจำ", mapped.PriceLabel, StringComparison.Ordinal);
        Assert.DoesNotContain("ราคาเต็ม", mapped.PriceLabel, StringComparison.Ordinal);
        Assert.Equal("พรีออเดอร์", mapped.TypeLabel);
        Assert.Equal("/products/test-product", mapped.ProductUrl);
        Assert.Equal(["/media/test.webp", "/media/test-2.webp"], mapped.GalleryImages.Select(image => image.Url));
    }

    [Fact]
    public void BrandDirectoryCompletesTheHeaderRouteWithPublishedCatalogData()
    {
        var source = WebSource("Components/Pages/BrandDirectory.razor");
        Assert.Contains("@page \"/brands\"", source, StringComparison.Ordinal);
        Assert.Contains("ListStorefrontProductsQuery", source, StringComparison.Ordinal);
        Assert.Contains("result.Value.Brands", source, StringComparison.Ordinal);
        Assert.Contains("/brands/{brand.Slug}", source, StringComparison.Ordinal);
        Assert.Contains("เลือกแบรนด์ก่อนดูสินค้า", source, StringComparison.Ordinal);
        Assert.Contains("สินค้าพร้อมส่งและพรีออเดอร์", source, StringComparison.Ordinal);
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
        Assert.Contains("StorefrontSaleTypeFilter.PreOrder", source, StringComparison.Ordinal);
        Assert.Contains("PageSize: 8", source, StringComparison.Ordinal);
        Assert.Contains("PageSize: 5", source, StringComparison.Ordinal);
        Assert.Contains("LoadingCardCount=\"8\"", source, StringComparison.Ordinal);
        Assert.Contains("preOrderProducts", source, StringComparison.Ordinal);
        Assert.Contains("preOrderState", source, StringComparison.Ordinal);
        Assert.Contains("OnRetry=\"LoadFeaturedAsync\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ActionUrl=\"/categories\"", source, StringComparison.Ordinal);
        Assert.DoesNotContain("/journal", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ลาบูบู้ นักผจญภัย", source, StringComparison.Ordinal);
        Assert.Contains("ProductCardModel.From", source, StringComparison.Ordinal);
        Assert.Contains("StorefrontStructuredData.BuildHome", source, StringComparison.Ordinal);
        Assert.Contains("CanonicalUrl", source, StringComparison.Ordinal);
    }

    private static string WebSource(string path) => File.ReadAllText(Path.Combine(Root(), "src", "ToyStore.Web", path));
    private static string Root()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln"))) return current.FullName;
        throw new DirectoryNotFoundException();
    }
}
