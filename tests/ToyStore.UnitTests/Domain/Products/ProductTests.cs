using System.Globalization;
using System.Reflection;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Products;

public sealed class ProductTests
{
    private static readonly Guid ProductId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CategoryId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BrandId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid UniverseId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void FactoryRejectsEmptyProductIdentity()
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(id: Guid.Empty));

        Assert.Equal(ProductRule.ProductIdentityRequired, exception.Rule);
    }

    [Theory]
    [InlineData("displayName")]
    [InlineData("englishName")]
    [InlineData("description")]
    public void FactoryRejectsBlankRequiredText(string field)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(
            displayName: field == "displayName" ? " " : "กันดั้ม เอ็กเซีย",
            englishName: field == "englishName" ? " " : "Gundam Exia",
            description: field == "description" ? " " : "โมเดลสะสม"));

        Assert.Equal(ProductRule.ProductTextRequired, exception.Rule);
    }

    [Fact]
    public void FactoryTrimsOptionalModelScale()
    {
        var product = CreateInStock(modelScale: " 1/12 ");

        Assert.Equal("1/12", product.ModelScale);
    }

    [Fact]
    public void FactoryRejectsModelScaleThatIsTooLong()
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(
            modelScale: new string('x', Product.MaximumModelScaleLength + 1)));

        Assert.Equal(ProductRule.ProductModelScaleInvalid, exception.Rule);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Gundam-Exia")]
    [InlineData("-gundam-exia")]
    [InlineData("gundam--exia")]
    [InlineData("gundam exia")]
    [InlineData("gundam-exia\n")]
    [InlineData("gundam-exia\r")]
    [InlineData("gundam-exia\u0000")]
    public void FactoryRejectsInvalidGeneratedSlug(string slug)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(slug: slug));

        Assert.Equal(ProductRule.ProductSlugInvalid, exception.Rule);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void PreOrderFactoryRejectsOfferClosedAtOrBeforeCreation(int secondsAfterClose)
    {
        var offer = PreOrderOffer.Create(
            Money.Create(2490),
            Money.Create(500),
            new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027),
            totalCapacity: 20,
            maxPerCustomer: 2,
            nowUtc: CreatedAtUtc);
        var createdAtUtc = offer.CloseAtUtc.AddSeconds(secondsAfterClose);

        var exception = Assert.Throws<ProductRuleException>(() => Product.CreatePreOrder(
            ProductId,
            "กันดั้ม เอ็กเซีย พรีออเดอร์",
            "Gundam Exia Pre Order",
            "โมเดลสะสมแบบพรีออเดอร์",
            "gundam-exia-pre-order",
            CategoryId,
            BrandId,
            UniverseId,
            offer,
            createdAtUtc,
            "admin-1"));

        Assert.Equal(ProductRule.PreOrderCloseMustBeFuture, exception.Rule);
    }

    [Theory]
    [InlineData("category")]
    [InlineData("brand")]
    [InlineData("universe")]
    public void FactoryRejectsEmptyRequiredRelation(string relation)
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(
            productCategoryId: relation == "category" ? Guid.Empty : CategoryId,
            brandId: relation == "brand" ? Guid.Empty : BrandId,
            universeId: relation == "universe" ? Guid.Empty : UniverseId));

        Assert.Equal(ProductRule.ProductRelationRequired, exception.Rule);
    }

    [Fact]
    public void FactoryRejectsNonUtcAuditInstant()
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(
            createdAtUtc: new DateTimeOffset(2026, 7, 17, 15, 0, 0, TimeSpan.FromHours(7))));

        Assert.Equal(ProductRule.UtcInstantRequired, exception.Rule);
    }

    [Fact]
    public void FactoryRejectsBlankAuditActor()
    {
        var exception = Assert.Throws<ProductRuleException>(() => CreateInStock(actor: " "));

        Assert.Equal(ProductRule.ProductActorRequired, exception.Rule);
    }

    [Fact]
    public void NewInStockProductIsDraftWithOnlyMatchingOfferAndInitialAudit()
    {
        var product = CreateInStock();

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(SaleType.InStock, product.SaleType);
        Assert.NotNull(product.InStockOffer);
        Assert.Null(product.PreOrderOffer);
        Assert.Empty(product.Images);
        Assert.Equal(CreatedAtUtc, product.CreatedAtUtc);
        Assert.Equal(CreatedAtUtc, product.UpdatedAtUtc);
        Assert.Equal("admin-1", product.CreatedBy);
        Assert.Equal("admin-1", product.UpdatedBy);
        Assert.Null(product.PublishedAtUtc);
        Assert.Null(product.ArchivedAtUtc);
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void CreationStoresTrimmedFormKcWhitespaceAndCultureInvariantNormalizedNames()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            var product = CreateInStock(
                displayName: "  กันดั้ม　   เอ็กเซีย  ",
                englishName: "  ｉstanbul   ＧＵＮＤＡＭ  ");

            Assert.Equal("กันดั้ม　   เอ็กเซีย", product.DisplayName);
            Assert.Equal("กันดั้ม เอ็กเซีย", product.NormalizedDisplayName);
            Assert.Equal("ｉstanbul   ＧＵＮＤＡＭ", product.EnglishName);
            Assert.Equal("ISTANBUL GUNDAM", product.NormalizedEnglishName);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void NewPreOrderProductIsDraftWithOnlyMatchingOffer()
    {
        var product = CreatePreOrder();

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(SaleType.PreOrder, product.SaleType);
        Assert.Null(product.InStockOffer);
        Assert.NotNull(product.PreOrderOffer);
    }

    [Fact]
    public void FactoriesRejectMissingConditionalOffer()
    {
        var inStockException = Assert.Throws<ProductRuleException>(() => Product.CreateInStock(
            ProductId,
            "กันดั้ม เอ็กเซีย",
            "Gundam Exia",
            "โมเดลสะสม",
            "gundam-exia",
            CategoryId,
            BrandId,
            UniverseId,
            null!,
            CreatedAtUtc,
            "admin-1"));
        var preOrderException = Assert.Throws<ProductRuleException>(() => Product.CreatePreOrder(
            ProductId,
            "กันดั้ม เอ็กเซีย",
            "Gundam Exia",
            "โมเดลสะสม",
            "gundam-exia",
            CategoryId,
            BrandId,
            UniverseId,
            null!,
            CreatedAtUtc,
            "admin-1"));

        Assert.Equal(ProductRule.ProductOfferMismatch, inStockException.Rule);
        Assert.Equal(ProductRule.ProductOfferMismatch, preOrderException.Rule);
    }

    [Fact]
    public void PublicMutationApiIsLimitedToDraftOwnedCollectionsAndLifecycleActions()
    {
        var constructors = typeof(Product).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        var mutations = typeof(Product)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToArray();

        Assert.Empty(constructors);
        Assert.All(
            typeof(Product).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
        Assert.Equal(
            ["Archive", "Publish", "UpdateDraftInStock", "UpdateDraftPreOrder"],
            mutations);
        Assert.NotNull(typeof(Product).GetField("_inStockOffer", BindingFlags.NonPublic | BindingFlags.Instance));
        Assert.NotNull(typeof(Product).GetField("_preOrderOffer", BindingFlags.NonPublic | BindingFlags.Instance));
    }

    [Fact]
    public void PublishRejectsDraftWithoutImage()
    {
        var product = CreateInStock();

        var exception = Assert.Throws<ProductRuleException>(
            () => product.Publish(product.Version, CreatedAtUtc, "admin-1"));

        Assert.Equal(ProductRule.ProductPublishRequiresImage, exception.Rule);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void ArchiveRejectsDraftProduct()
    {
        var product = CreateInStock();

        var exception = Assert.Throws<ProductRuleException>(
            () => product.Archive(product.Version, CreatedAtUtc, "admin-1"));

        Assert.Equal(ProductRule.ProductTransitionInvalid, exception.Rule);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(1, product.Version);
    }

    [Theory]
    [InlineData("nonUtc", ProductRule.UtcInstantRequired)]
    [InlineData("backwards", ProductRule.ProductAuditTimeWentBackwards)]
    [InlineData("actor", ProductRule.ProductActorRequired)]
    public void PublishRejectsInvalidAuditDataWithoutChangingState(
        string invalidField,
        ProductRule expectedRule)
    {
        var product = CreateInStock(images: [Image()]);
        var publishedAt = invalidField switch
        {
            "nonUtc" => new DateTimeOffset(2026, 7, 17, 16, 0, 0, TimeSpan.FromHours(7)),
            "backwards" => CreatedAtUtc.AddTicks(-1),
            _ => CreatedAtUtc,
        };

        var exception = Assert.Throws<ProductRuleException>(() => product.Publish(
            product.Version,
            publishedAt,
            invalidField == "actor" ? " " : "admin-2"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Null(product.PublishedAtUtc);
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void PublishRecordsAuditAndCannotBeRepeated()
    {
        var product = CreateInStock(images: [Image()]);
        var publishedAt = CreatedAtUtc.AddHours(1);

        product.Publish(product.Version, publishedAt, "admin-2");

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(publishedAt, product.PublishedAtUtc);
        Assert.Equal("admin-2", product.PublishedBy);
        Assert.Equal(publishedAt, product.UpdatedAtUtc);
        Assert.Equal("admin-2", product.UpdatedBy);
        Assert.Equal(2, product.Version);
        var exception = Assert.Throws<ProductRuleException>(
            () => product.Publish(product.Version, publishedAt, "admin-2"));
        Assert.Equal(ProductRule.ProductTransitionInvalid, exception.Rule);
        Assert.Equal(2, product.Version);
    }

    [Theory]
    [InlineData("nonUtc", ProductRule.UtcInstantRequired)]
    [InlineData("backwards", ProductRule.ProductAuditTimeWentBackwards)]
    [InlineData("actor", ProductRule.ProductActorRequired)]
    public void ArchiveRejectsInvalidAuditDataWithoutChangingState(
        string invalidField,
        ProductRule expectedRule)
    {
        var product = CreateInStock(images: [Image()]);
        product.Publish(product.Version, CreatedAtUtc, "admin-1");
        var archivedAt = invalidField switch
        {
            "nonUtc" => new DateTimeOffset(2026, 7, 17, 15, 0, 0, TimeSpan.FromHours(7)),
            "backwards" => CreatedAtUtc.AddTicks(-1),
            _ => CreatedAtUtc,
        };

        var exception = Assert.Throws<ProductRuleException>(() => product.Archive(
            product.Version,
            archivedAt,
            invalidField == "actor" ? " " : "admin-2"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Null(product.ArchivedAtUtc);
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public void ArchiveAcceptsEqualTimestampAndRecordsAudit()
    {
        var product = CreateInStock(images: [Image()]);
        product.Publish(product.Version, CreatedAtUtc, "admin-1");

        product.Archive(product.Version, CreatedAtUtc, "admin-2");

        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Equal(CreatedAtUtc, product.ArchivedAtUtc);
        Assert.Equal("admin-2", product.ArchivedBy);
        Assert.Equal(CreatedAtUtc, product.UpdatedAtUtc);
        Assert.Equal("admin-2", product.UpdatedBy);
        Assert.Equal(3, product.Version);
    }

    private static Product CreateInStock(
        Guid? id = null,
        string displayName = "กันดั้ม เอ็กเซีย",
        string englishName = "Gundam Exia",
        string description = "โมเดลสะสม",
        string slug = "gundam-exia",
        Guid? productCategoryId = null,
        Guid? brandId = null,
        Guid? universeId = null,
        DateTimeOffset? createdAtUtc = null,
        string actor = "admin-1",
        IReadOnlyCollection<ProductImageDefinition>? images = null,
        string? modelScale = null) =>
        Product.CreateInStock(
            id ?? ProductId,
            displayName,
            englishName,
            description,
            slug,
            productCategoryId ?? CategoryId,
            brandId ?? BrandId,
            universeId ?? UniverseId,
            InStockOffer.Create(Money.Create(1490)),
            images ?? [],
            [],
            createdAtUtc ?? CreatedAtUtc,
            actor,
            modelScale);

    private static Product CreatePreOrder() =>
        Product.CreatePreOrder(
            ProductId,
            "กันดั้ม เอ็กเซีย พรีออเดอร์",
            "Gundam Exia Pre Order",
            "โมเดลสะสมแบบพรีออเดอร์",
            "gundam-exia-pre-order",
            CategoryId,
            BrandId,
            UniverseId,
            PreOrderOffer.Create(
                Money.Create(2490),
                Money.Create(500),
                new DateOnly(2026, 12, 31),
                EstimatedArrival.Create(1, 2027),
                totalCapacity: 20,
                maxPerCustomer: 2,
                nowUtc: CreatedAtUtc),
            CreatedAtUtc,
            "admin-1");

    private static ProductImageDefinition Image() => new(
            Guid.NewGuid(),
            "products/gundam-exia/front.webp",
            "/media/products/gundam-exia/front.webp",
            "กันดั้ม เอ็กเซีย มุมด้านหน้า");
}
