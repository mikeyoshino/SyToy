using System.Text.Json;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Web.Components.Seo;
using ToyStore.Web.Seo;

namespace ToyStore.UnitTests.Web;

public sealed class StorefrontSeoTests
{
    [Fact]
    public void HomeStructuredDataDeclaresSiteOrganizationAndServerVisiblePage()
    {
        using var document = JsonDocument.Parse(StorefrontStructuredData.BuildHome(
            new Uri("https://sytoys.shop/"),
            new Uri("https://sytoys.shop/images/brand/sy-toys-mark-v2.png"),
            new Uri("https://sytoys.shop/media/product.webp"),
            "ร้านอาร์ตทอย และ กันดั้ม"));

        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
        Assert.Equal("https://schema.org", document.RootElement.GetProperty("@context").GetString());
        Assert.Contains(graph, node => node.GetProperty("@type").GetString() == "Organization");
        Assert.Contains(graph, node => node.GetProperty("@type").GetString() == "WebSite");
        Assert.Contains(graph, node => node.GetProperty("@type").GetString() == "WebPage");
        var website = graph.Single(node => node.GetProperty("@type").GetString() == "WebSite");
        Assert.Equal("SY TOYS", website.GetProperty("name").GetString());
        Assert.Equal("th-TH", website.GetProperty("inLanguage").GetString());
    }

    [Fact]
    public void InStockProductUsesFullPriceAvailabilityFreeThailandShippingAndBreadcrumbs()
    {
        using var document = JsonDocument.Parse(StorefrontStructuredData.BuildProduct(
            Product(StorefrontSaleType.InStock, StorefrontOfferState.InStockAvailable),
            new Uri("https://sytoys.shop/products/robot"),
            "หุ่นยนต์สะสม รุ่นทดสอบ"));

        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
        var product = graph.Single(node => node.GetProperty("@type").GetString() == "Product");
        var offer = product.GetProperty("offers");
        Assert.Equal(2500m, offer.GetProperty("price").GetDecimal());
        Assert.Equal("THB", offer.GetProperty("priceCurrency").GetString());
        Assert.Equal("https://schema.org/InStock", offer.GetProperty("availability").GetString());
        Assert.Equal(0, offer.GetProperty("shippingDetails").GetProperty("shippingRate").GetProperty("value").GetInt32());
        Assert.Equal("TH", offer.GetProperty("shippingDetails").GetProperty("shippingDestination").GetProperty("addressCountry").GetString());
        Assert.Equal(3, graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").GetArrayLength());
        Assert.False(product.TryGetProperty("review", out _));
        Assert.False(product.TryGetProperty("aggregateRating", out _));
        Assert.False(product.TryGetProperty("gtin", out _));
    }

    [Fact]
    public void OpenPreOrderUsesFullProductPricePreOrderAvailabilityAndBangkokCloseDate()
    {
        var product = Product(StorefrontSaleType.PreOrder, StorefrontOfferState.PreOrderOpen) with
        {
            PreOrderCloseAtUtc = new DateTimeOffset(2026, 7, 20, 18, 0, 0, TimeSpan.Zero),
        };
        using var document = JsonDocument.Parse(StorefrontStructuredData.BuildProduct(
            product,
            new Uri("https://sytoys.shop/products/robot"),
            product.Description));
        var offer = document.RootElement.GetProperty("@graph").EnumerateArray()
            .Single(node => node.GetProperty("@type").GetString() == "Product")
            .GetProperty("offers");

        Assert.Equal(2500m, offer.GetProperty("price").GetDecimal());
        Assert.NotEqual(product.DepositAmount, offer.GetProperty("price").GetDecimal());
        Assert.Equal("https://schema.org/PreOrder", offer.GetProperty("availability").GetString());
        Assert.Equal("2026-07-21", offer.GetProperty("priceValidUntil").GetString());
        Assert.False(offer.TryGetProperty("shippingDetails", out _));
    }

    [Fact]
    public void ContactStructuredDataExposesStoreIdentityAndCustomerServiceDetails()
    {
        using var document = JsonDocument.Parse(StorefrontStructuredData.BuildContact(
            new Uri("https://sytoys.shop/contact"),
            new Uri("https://sytoys.shop/images/brand/sy-toys-mark-v2.png"),
            "https://www.facebook.com/sytoysofficial/"));

        var graph = document.RootElement.GetProperty("@graph").EnumerateArray().ToArray();
        var organization = graph.Single(node => node.GetProperty("@type").GetString() == "Organization");
        var contactPage = graph.Single(node => node.GetProperty("@type").GetString() == "ContactPage");

        Assert.Equal("+66-98-254-0399", organization.GetProperty("telephone").GetString());
        Assert.Equal("sytoys.official@gmail.com", organization.GetProperty("email").GetString());
        Assert.Equal("50140", organization.GetProperty("address").GetProperty("postalCode").GetString());
        Assert.Equal("Thai", organization.GetProperty("contactPoint").GetProperty("availableLanguage")[0].GetString());
        Assert.Equal("https://sytoys.shop/contact", contactPage.GetProperty("url").GetString());
        Assert.Equal(2, graph.Single(node => node.GetProperty("@type").GetString() == "BreadcrumbList")
            .GetProperty("itemListElement").GetArrayLength());
    }

    [Fact]
    public void DiscoveryFilesExposeCanonicalSitemapAndKeepPrivateFlowsOutOfCrawlPaths()
    {
        var robots = SeoEndpointExtensions.BuildRobots("https://sytoys.shop");
        var sitemap = SeoEndpointExtensions.BuildSitemap(
            ["https://sytoys.shop/", "https://sytoys.shop/products/a&b"]);

        Assert.Contains("Sitemap: https://sytoys.shop/sitemap.xml", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /admin/", robots, StringComparison.Ordinal);
        Assert.Contains("Disallow: /checkout/", robots, StringComparison.Ordinal);
        Assert.Contains("xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\"", sitemap, StringComparison.Ordinal);
        Assert.Contains("https://sytoys.shop/products/a&amp;b", sitemap, StringComparison.Ordinal);
        Assert.DoesNotContain("encoding=\"utf-16\"", sitemap, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void HomeAndProductPagesUseSharedCompleteMetadataInServerRenderedMarkup()
    {
        var root = RepositoryRoot();
        var home = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web", "Components", "Pages", "Home.razor"));
        var detail = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web", "Components", "Pages", "ProductDetail.razor"));
        var metadata = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web", "Components", "Seo", "StorefrontSeoMetadata.razor"));
        var program = File.ReadAllText(Path.Combine(root, "src", "ToyStore.Web", "Program.cs"));

        Assert.Contains("<StorefrontSeoMetadata", home, StringComparison.Ordinal);
        Assert.Contains("<StorefrontSeoMetadata", detail, StringComparison.Ordinal);
        Assert.Contains("StructuredDataJson", home, StringComparison.Ordinal);
        Assert.Contains("StructuredDataJson", detail, StringComparison.Ordinal);
        Assert.Contains("application/ld+json", metadata, StringComparison.Ordinal);
        Assert.Contains("og:url", metadata, StringComparison.Ordinal);
        Assert.Contains("twitter:card", metadata, StringComparison.Ordinal);
        Assert.Contains("max-image-preview:large", metadata, StringComparison.Ordinal);
        Assert.Contains("noindex,follow", detail, StringComparison.Ordinal);
        Assert.Contains("app.MapStorefrontSeo()", program, StringComparison.Ordinal);
    }

    private static StorefrontProductDetail Product(
        StorefrontSaleType saleType,
        StorefrontOfferState offerState) => new(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            "หุ่นยนต์สะสม",
            "Collectible Robot",
            "รายละเอียด สินค้า\nที่มองเห็นบนหน้า",
            "robot",
            "แบรนด์ทดสอบ",
            "test-brand",
            "กันดั้ม",
            "จักรวาลทดสอบ",
            ["ตัวละครทดสอบ"],
            saleType,
            offerState,
            2500m,
            saleType == StorefrontSaleType.PreOrder ? 500m : null,
            saleType == StorefrontSaleType.PreOrder ? 2000m : null,
            saleType == StorefrontSaleType.InStock ? 5 : 0,
            saleType == StorefrontSaleType.PreOrder ? new DateTimeOffset(2026, 7, 20, 18, 0, 0, TimeSpan.Zero) : null,
            saleType == StorefrontSaleType.PreOrder ? 12 : null,
            saleType == StorefrontSaleType.PreOrder ? 2026 : null,
            saleType == StorefrontSaleType.PreOrder ? 2 : null,
            saleType == StorefrontSaleType.PreOrder ? 7 : null,
            [new StorefrontProductImage("/media/robot.webp", "หุ่นยนต์สะสม ด้านหน้า", 0, true)],
            "1/100");

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory); current is not null; current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
