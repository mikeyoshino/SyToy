using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Products;

public sealed class ProductOfferTests
{
    [Fact]
    public void SaleAndLifecycleEnumsExposeOnlyApprovedValues()
    {
        Assert.Equal([SaleType.InStock, SaleType.PreOrder], Enum.GetValues<SaleType>());
        Assert.Equal(
            [ProductStatus.Draft, ProductStatus.Published, ProductStatus.Archived],
            Enum.GetValues<ProductStatus>());
    }

    [Fact]
    public void MoneyRejectsNegativeAmountWithStableRule()
    {
        var exception = Assert.Throws<ProductRuleException>(() => Money.Create(-0.01m));

        Assert.Equal(ProductRule.MoneyCannotBeNegative, exception.Rule);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(123.4567)]
    public void MoneyPreservesNonNegativeAmountAndUsesThb(decimal amount)
    {
        var money = Money.Create(amount);

        Assert.Equal(amount, money.Amount);
        Assert.Equal("THB", Money.Currency);
    }

    [Fact]
    public void InStockOfferRejectsZeroPriceWithStableRule()
    {
        var exception = Assert.Throws<ProductRuleException>(
            () => InStockOffer.Create(Money.Create(0)));

        Assert.Equal(ProductRule.InStockPriceMustBePositive, exception.Rule);
    }

    [Fact]
    public void InStockOfferPreservesPositivePrice()
    {
        var offer = InStockOffer.Create(Money.Create(1490.25m));

        Assert.Equal(1490.25m, offer.Price.Amount);
    }

    [Fact]
    public void PreOrderCloseDateBecomesBangkokEndOfDayStoredAsUtc()
    {
        var offer = PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(200),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(12, 2026),
            totalCapacity: 10,
            maxPerCustomer: 2,
            nowUtc: new DateTimeOffset(2026, 12, 31, 16, 59, 58, TimeSpan.Zero));

        Assert.Equal(
            new DateTimeOffset(2026, 12, 31, 16, 59, 59, TimeSpan.Zero),
            offer.CloseAtUtc);
        Assert.Equal(TimeSpan.Zero, offer.CloseAtUtc.Offset);
    }

    [Fact]
    public void PreOrderOfferRejectsNonUtcCurrentInstant()
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreatePreOrder(
            closeDate: new DateOnly(2026, 12, 31),
            nowUtc: new DateTimeOffset(2026, 12, 31, 20, 0, 0, TimeSpan.FromHours(7))));

        Assert.Equal(ProductRule.UtcInstantRequired, exception.Rule);
    }

    [Theory]
    [InlineData(16, 59, 59)]
    [InlineData(17, 0, 0)]
    public void PreOrderOfferRequiresCloseInstantStrictlyAfterNow(int hour, int minute, int second)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreatePreOrder(
            closeDate: new DateOnly(2026, 12, 31),
            nowUtc: new DateTimeOffset(2026, 12, 31, hour, minute, second, TimeSpan.Zero)));

        Assert.Equal(ProductRule.PreOrderCloseMustBeFuture, exception.Rule);
    }

    [Theory]
    [InlineData(0, 2026)]
    [InlineData(13, 2026)]
    [InlineData(12, 0)]
    [InlineData(12, 10000)]
    public void EstimatedArrivalRejectsInvalidMonthOrYear(int month, int year)
    {
        var exception = Assert.Throws<ProductRuleException>(
            () => EstimatedArrival.Create(month, year));

        Assert.Equal(ProductRule.EstimatedArrivalInvalid, exception.Rule);
    }

    [Fact]
    public void EstimatedArrivalCannotPrecedeSelectedBangkokCloseMonth()
    {
        var exception = Assert.Throws<ProductRuleException>(() => PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(200),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(11, 2026),
            totalCapacity: 10,
            maxPerCustomer: 2,
            nowUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero)));

        Assert.Equal(ProductRule.EstimatedArrivalBeforeClose, exception.Rule);
    }

    [Fact]
    public void EstimatedArrivalComparisonHandlesDecemberToJanuaryRollover()
    {
        var offer = PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(200),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027),
            totalCapacity: 10,
            maxPerCustomer: 2,
            nowUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(1, offer.EstimatedArrival.Month);
        Assert.Equal(2027, offer.EstimatedArrival.Year);
    }

    [Theory]
    [InlineData(0, 1, ProductRule.PreOrderFullPriceMustBePositive)]
    [InlineData(1000, 0, ProductRule.PreOrderDepositMustBePositive)]
    [InlineData(1000, 1000, ProductRule.PreOrderDepositMustBeBelowFullPrice)]
    [InlineData(1000, 1100, ProductRule.PreOrderDepositMustBeBelowFullPrice)]
    public void PreOrderOfferRejectsInvalidAmounts(
        decimal fullPrice,
        decimal depositAmount,
        ProductRule expectedRule)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreatePreOrder(
            fullPrice,
            depositAmount,
            totalCapacity: 10,
            maxPerCustomer: 2,
            balancePaymentDays: 7));

        Assert.Equal(expectedRule, exception.Rule);
    }

    [Theory]
    [InlineData(0, 1, 7, ProductRule.PreOrderCapacityMustBePositive)]
    [InlineData(10, 0, 7, ProductRule.PreOrderMaxPerCustomerMustBePositive)]
    [InlineData(10, 11, 7, ProductRule.PreOrderMaxPerCustomerExceedsCapacity)]
    [InlineData(10, 2, 0, ProductRule.PreOrderBalancePaymentDaysMustBePositive)]
    public void PreOrderOfferRejectsInvalidCapacityAndPaymentWindow(
        int totalCapacity,
        int maxPerCustomer,
        int balancePaymentDays,
        ProductRule expectedRule)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreatePreOrder(
            fullPrice: 1000,
            depositAmount: 200,
            totalCapacity,
            maxPerCustomer,
            balancePaymentDays));

        Assert.Equal(expectedRule, exception.Rule);
    }

    [Fact]
    public void PreOrderOfferComputesBalanceAndDefaultsPaymentWindowToSevenDays()
    {
        var offer = PreOrderOffer.Create(
            Money.Create(1234.56m),
            Money.Create(234.12m),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027),
            totalCapacity: 10,
            maxPerCustomer: 2,
            nowUtc: new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(1000.44m, offer.BalanceAmount.Amount);
        Assert.Equal(7, offer.BalancePaymentDays);
        Assert.Equal(10, offer.TotalCapacity);
        Assert.Equal(2, offer.MaxPerCustomer);
    }

    [Fact]
    public void OfferValuesExposeNoPublicInvalidConstructorOrWritableState()
    {
        var valueTypes = new[]
        {
            typeof(Money),
            typeof(EstimatedArrival),
            typeof(InStockOffer),
            typeof(PreOrderOffer),
        };

        Assert.All(valueTypes, type =>
        {
            Assert.Empty(type.GetConstructors());
            Assert.All(
                type.GetProperties(),
                property => Assert.False(property.SetMethod?.IsPublic ?? false));
        });
        Assert.Null(typeof(PreOrderOffer).GetProperty(nameof(PreOrderOffer.BalanceAmount))!.SetMethod);
    }

    [Fact]
    public void PreOrderProductCanUpdateDraftAndPublishWithoutChangingSaleType()
    {
        var now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        var offer = CreatePreOrder(1000, 200, 10, 2, 7);
        var product = Product.CreatePreOrder(
            Guid.NewGuid(), "สินค้า", "Product", "รายละเอียด", "product",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), offer,
            [new ProductImageDefinition(Guid.NewGuid(), "batch/main.webp", "/media/main.webp", "สินค้า")],
            [], now, "admin");
        var replacement = PreOrderOffer.Create(
            Money.Create(1200), Money.Create(300), new DateOnly(2027, 1, 31),
            EstimatedArrival.Create(2, 2027), 12, 3, now, 7);

        product.UpdateDraftPreOrder(
            "สินค้าใหม่", "Product New", "รายละเอียดใหม่", "product-new",
            product.ProductCategoryId, product.BrandId, product.UniverseId,
            replacement,
            product.Images.Select(image => new ProductImageDefinition(
                image.Id, image.StorageKey, image.PublicRelativeUrl, image.AltText)).ToArray(),
            [], product.Version, now.AddMinutes(1), "admin");
        product.Publish(product.Version, now.AddMinutes(2), "admin");

        Assert.Equal(SaleType.PreOrder, product.SaleType);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(1200, product.PreOrderOffer!.FullPrice.Amount);
        Assert.Null(product.InStockOffer);
    }

    private static PreOrderOffer CreatePreOrder(DateOnly closeDate, DateTimeOffset nowUtc) =>
        PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(200),
            closeDate,
            EstimatedArrival.Create(closeDate.Month, closeDate.Year),
            totalCapacity: 10,
            maxPerCustomer: 2,
            nowUtc);

    private static PreOrderOffer CreatePreOrder(
        decimal fullPrice,
        decimal depositAmount,
        int totalCapacity,
        int maxPerCustomer,
        int balancePaymentDays) =>
        PreOrderOffer.Create(
            Money.Create(fullPrice),
            Money.Create(depositAmount),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027),
            totalCapacity,
            maxPerCustomer,
            new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            balancePaymentDays);
}
