using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Web.Components.Cart;
using ToyStore.Web.Components.Storefront;
using ToyStore.Web.Components.Storefront.Models;

namespace ToyStore.UnitTests.Web;

public sealed class ProductGalleryRenderingTests
{
    private static readonly ProductCardModel AvailableProduct = new(
        Guid.NewGuid(),
        "มอลลี่ นักบินอวกาศ",
        "ป๊อป มาร์ท",
        "฿3,590",
        "สินค้าพร้อมส่ง",
        null,
        "/products/molly-astronaut",
        StorefrontSaleType.InStock,
        StorefrontOfferState.InStockAvailable);

    private static readonly ProductCardModel UnavailableProduct = new(
        Guid.NewGuid(),
        "แบทแมน แบล็กเอดิชัน",
        "ดีซี คอลเล็กทิเบิลส์",
        "฿4,590",
        "พรีออเดอร์",
        null,
        "/products/batman-black-edition",
        StorefrontSaleType.PreOrder,
        StorefrontOfferState.PreOrderClosed);

    [Fact]
    public async Task NormalRendersEveryKeyedProductWithAccurateActions()
    {
        var html = await RenderGalleryAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ProductGallery.Products)] = new[] { AvailableProduct, UnavailableProduct },
                [nameof(ProductGallery.State)] = StorefrontContentState.Normal,
            }));

        Assert.Equal(2, CountOccurrences(html, "<article"));
        Assert.Contains(AvailableProduct.Name, html, StringComparison.Ordinal);
        Assert.Contains(UnavailableProduct.Name, html, StringComparison.Ordinal);
        Assert.Contains("aria-label=\"เพิ่ม มอลลี่ นักบินอวกาศ ลงตะกร้า\"", html, StringComparison.Ordinal);
        Assert.Contains("ปิดรับพรีออเดอร์แล้ว", html, StringComparison.Ordinal);
        Assert.Contains("เพิ่มลงตะกร้า", html, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(html, "เพิ่มลงตะกร้า"));
    }

    [Fact]
    public async Task LoadingRendersStableSkeletonCardsAndScreenReaderStatus()
    {
        var html = await RenderGalleryAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ProductGallery.State)] = StorefrontContentState.Loading,
                [nameof(ProductGallery.LoadingCardCount)] = 3,
            }));

        Assert.Equal(3, CountOccurrences(html, "<article"));
        Assert.Equal(3, CountOccurrences(html, "product-card__media product-card__skeleton"));
        Assert.Contains("class=\"product-gallery\" aria-hidden=\"true\"", html, StringComparison.Ordinal);
        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("กำลังโหลดรายการสินค้า", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EmptyRendersStatusCopyWithoutAlert()
    {
        var html = await RenderGalleryAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ProductGallery.State)] = StorefrontContentState.Empty,
            }));

        Assert.Contains("role=\"status\"", html, StringComparison.Ordinal);
        Assert.Contains("ยังไม่มีสินค้าในหมวดนี้", html, StringComparison.Ordinal);
        Assert.DoesNotContain("role=\"alert\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ErrorRendersAlertAndRetryOnlyWhenCallbackIsProvided()
    {
        var retryCallback = EventCallback.Factory.Create(this, () => { });
        var withRetry = await RenderGalleryAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ProductGallery.State)] = StorefrontContentState.Error,
                [nameof(ProductGallery.OnRetry)] = retryCallback,
            }));
        var withoutRetry = await RenderGalleryAsync(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(ProductGallery.State)] = StorefrontContentState.Error,
            }));

        Assert.Contains("role=\"alert\"", withRetry, StringComparison.Ordinal);
        Assert.Contains("โหลดสินค้าไม่สำเร็จ", withRetry, StringComparison.Ordinal);
        Assert.Matches(@"<button\b[^>]*>ลองอีกครั้ง</button>", withRetry);
        Assert.Contains("role=\"alert\"", withoutRetry, StringComparison.Ordinal);
        Assert.DoesNotMatch(@"<button\b[^>]*>ลองอีกครั้ง</button>", withoutRetry);
    }

    [Fact]
    public async Task RetryAsyncInvokesTheSuppliedCallbackExactlyOnce()
    {
        var invocationCount = 0;
#pragma warning disable BL0005 // Direct assignment is intentional for the component callback unit test.
        var gallery = new ProductGallery
        {
            OnRetry = EventCallback.Factory.Create(this, () => invocationCount++),
        };
#pragma warning restore BL0005

        await gallery.RetryAsync();

        Assert.Equal(1, invocationCount);
    }

    [Theory]
    [InlineData(2, "h2")]
    [InlineData(3, "h3")]
    public async Task SectionHeaderPlacesRegionIdOnTheSemanticHeading(
        int headingLevel,
        string expectedElement)
    {
        var html = await RenderComponentAsync<SectionHeader>(ParameterView.FromDictionary(
            new Dictionary<string, object?>
            {
                [nameof(SectionHeader.Title)] = "สินค้าแนะนำ",
                ["HeadingId"] = "products-heading",
                [nameof(SectionHeader.HeadingLevel)] = headingLevel,
                [nameof(SectionHeader.ActionLabel)] = "ดูทั้งหมด",
                [nameof(SectionHeader.ActionUrl)] = "/products",
            }));

        Assert.Matches(
            $@"<{expectedElement}\b[^>]*id=""products-heading""[^>]*>สินค้าแนะนำ</{expectedElement}>",
            html);
        Assert.DoesNotMatch(@"<h[23][^>]*>[^<]*ดูทั้งหมด", html);
    }

    private static async Task<string> RenderGalleryAsync(ParameterView parameters)
        => await RenderComponentAsync<ProductGallery>(parameters);

    private static async Task<string> RenderComponentAsync<TComponent>(ParameterView parameters)
        where TComponent : IComponent
    {
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddSingleton<CartDrawerCoordinator>();

        await using var serviceProvider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(
            serviceProvider,
            serviceProvider.GetRequiredService<ILoggerFactory>());

        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var output = await renderer.RenderComponentAsync<TComponent>(parameters);
            return WebUtility.HtmlDecode(output.ToHtmlString());
        });
    }

    private static int CountOccurrences(string source, string value) =>
        Regex.Count(source, Regex.Escape(value), RegexOptions.CultureInvariant);
}
