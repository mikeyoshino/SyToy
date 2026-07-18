using ToyStore.Application.Storefront.Catalog;

namespace ToyStore.UnitTests.Application.Products;

public sealed class StorefrontCatalogQueryTests
{
    [Fact]
    public void StorefrontContractsExposeSaleTypeOfferStateAndPreOrderSchedule()
    {
        Assert.Equal(
            [StorefrontSaleType.InStock, StorefrontSaleType.PreOrder],
            Enum.GetValues<StorefrontSaleType>());
        Assert.Equal(
            [StorefrontOfferState.InStockAvailable, StorefrontOfferState.InStockOutOfStock,
             StorefrontOfferState.PreOrderOpen, StorefrontOfferState.PreOrderClosed,
             StorefrontOfferState.PreOrderFull],
            Enum.GetValues<StorefrontOfferState>());

        var card = new StorefrontProductCard(
            Guid.NewGuid(), "สินค้า", "product", "Brand", "อาร์ตทอย",
            StorefrontSaleType.PreOrder, StorefrontOfferState.PreOrderOpen,
            1000, 200, 3, "/media/product.webp", "รูปสินค้า");

        Assert.Equal(1000, card.Price);
        Assert.Equal(200, card.DepositAmount);
        Assert.True(card.IsAvailable);
    }

    [Fact]
    public void ListValidatorRejectsMalformedIdsPriceRangePageAndTypeWithThaiMessages()
    {
        var result = new ListStorefrontProductsValidator().Validate(new ListStorefrontProductsQuery(
            Search: new string('ก', 201), SaleType: (StorefrontSaleTypeFilter)99,
            BrandSlug: "ไม่ใช่ slug",
            ProductCategoryId: Guid.Empty, BrandId: Guid.Empty, CharacterId: Guid.Empty,
            UniverseId: Guid.Empty, MinimumPrice: 500, MaximumPrice: 100, Page: 0, PageSize: 49));

        Assert.False(result.IsValid);
        Assert.All(result.Errors, failure => Assert.False(string.IsNullOrWhiteSpace(failure.ErrorMessage)));
        Assert.Contains(result.Errors, failure => failure.ErrorMessage.Contains("ช่วงราคา", StringComparison.Ordinal));
        Assert.Contains(result.Errors, failure => failure.ErrorMessage.Contains("ประเภทการขาย", StringComparison.Ordinal));
        Assert.Contains(result.Errors, failure => failure.ErrorMessage.Contains("URL ของแบรนด์", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ListHandlerNormalizesAndPassesEveryFilterWithUtcNow()
    {
        var reader = new CapturingReader();
        var now = new DateTimeOffset(2026, 7, 17, 6, 0, 0, TimeSpan.Zero);
        var handler = new ListStorefrontProductsHandler(reader, new FixedTimeProvider(now));
        var category = Guid.NewGuid(); var brand = Guid.NewGuid(); var character = Guid.NewGuid(); var universe = Guid.NewGuid();

        var result = await handler.Handle(new ListStorefrontProductsQuery(
            "  สินค้า  หลัก ", StorefrontSaleTypeFilter.InStock, category, brand,
            " Public-Brand ", character, universe, 100, 2000, 2, 12), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(reader.ListRequest);
        Assert.Equal(category, reader.ListRequest.ProductCategoryId);
        Assert.Equal(brand, reader.ListRequest.BrandId);
        Assert.Equal("public-brand", reader.ListRequest.BrandSlug);
        Assert.Equal(character, reader.ListRequest.CharacterId);
        Assert.Equal(universe, reader.ListRequest.UniverseId);
        Assert.Equal(now, reader.ListRequest.NowUtc);
        Assert.Equal(2, result.Value.PageNumber);
    }

    [Fact]
    public async Task DetailHandlerReturnsTypedNotFoundAndNeverInventsProduct()
    {
        var handler = new GetStorefrontProductHandler(new CapturingReader(), new FixedTimeProvider(DateTimeOffset.UtcNow));
        var result = await handler.Handle(new GetStorefrontProductQuery("missing-product"), CancellationToken.None);
        Assert.Equal(StorefrontCatalogErrors.ProductNotFound, result.Error);
    }

    private sealed class CapturingReader : IStorefrontCatalogReader
    {
        public StorefrontCatalogReadRequest? ListRequest { get; private set; }
        public Task<StorefrontCatalogReadPage> ListAsync(StorefrontCatalogReadRequest request, CancellationToken cancellationToken)
        {
            ListRequest = request;
            return Task.FromResult(new StorefrontCatalogReadPage([], [], [], [], [], null, request.PageNumber, 0));
        }
        public Task<StorefrontProductDetail?> FindBySlugAsync(string slug, DateTimeOffset nowUtc, CancellationToken cancellationToken) =>
            Task.FromResult<StorefrontProductDetail?>(null);
    }
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
