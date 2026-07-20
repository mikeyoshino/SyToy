using System.Reflection;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Products;

public sealed class ProductVersionedEditingTests
{
    private static readonly Guid ProductId =
        Guid.Parse("71000000-0000-0000-0000-000000000001");
    private static readonly Guid CategoryId =
        Guid.Parse("71000000-0000-0000-0000-000000000002");
    private static readonly Guid BrandId =
        Guid.Parse("71000000-0000-0000-0000-000000000003");
    private static readonly Guid UniverseId =
        Guid.Parse("71000000-0000-0000-0000-000000000004");
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CompleteCreationStartsAtPositiveVersionOneForBothSaleTypes()
    {
        var images = new[] { Image(Guid.NewGuid(), "front.webp") };
        var characters = new[] { Guid.NewGuid() };

        var inStock = CreateInStock(images, characters);
        var preOrder = CreatePreOrder(images, characters);

        Assert.Equal(1, inStock.Version);
        Assert.Equal(1, preOrder.Version);
        Assert.Single(inStock.Images);
        Assert.Single(inStock.Characters);
    }

    [Fact]
    public void AtomicDraftInStockUpdateReplacesAllEditableContentWithOneIncrement()
    {
        var product = CreateInStock();
        var changedAtUtc = CreatedAtUtc.AddMinutes(1);
        var replacementCategoryId = Guid.NewGuid();
        var replacementBrandId = Guid.NewGuid();
        var replacementUniverseId = Guid.NewGuid();
        var firstImageId = Guid.NewGuid();
        var secondImageId = Guid.NewGuid();
        var characters = new[] { Guid.NewGuid(), Guid.NewGuid() };

        product.UpdateDraftInStock(
            "  กันดั้ม บาร์บาทอส  ",
            "  Gundam Barbatos  ",
            "  โมเดลเวอร์ชันใหม่  ",
            "gundam-barbatos",
            replacementCategoryId,
            replacementBrandId,
            replacementUniverseId,
            InStockOffer.Create(Money.Create(1890)),
            [Image(firstImageId, "front.webp"), Image(secondImageId, "back.webp")],
            characters,
            expectedVersion: 1,
            changedAtUtc,
            "admin-2");

        Assert.Equal("กันดั้ม บาร์บาทอส", product.DisplayName);
        Assert.Equal("GUNDAM BARBATOS", product.NormalizedEnglishName);
        Assert.Equal("โมเดลเวอร์ชันใหม่", product.Description);
        Assert.Equal("gundam-barbatos", product.Slug);
        Assert.Equal(replacementCategoryId, product.ProductCategoryId);
        Assert.Equal(replacementBrandId, product.BrandId);
        Assert.Equal(replacementUniverseId, product.UniverseId);
        Assert.Equal(1890, product.InStockOffer!.Price.Amount);
        Assert.Equal([firstImageId, secondImageId], product.Images.Select(image => image.Id));
        Assert.Equal([0, 1], product.Images.Select(image => image.SortOrder));
        Assert.True(product.Images[0].IsPrimary);
        Assert.Equal(characters.Order(), product.Characters.Select(link => link.CharacterId));
        Assert.Equal(2, product.Version);
        Assert.Equal(changedAtUtc, product.UpdatedAtUtc);
        Assert.Equal("admin-2", product.UpdatedBy);
    }

    [Fact]
    public void AtomicReplacementReusesRetainedImageAndCharacterInstances()
    {
        var removedImage = Image(Guid.NewGuid(), "removed.webp");
        var retainedImage = Image(Guid.NewGuid(), "retained.webp");
        var removedCharacterId = Guid.NewGuid();
        var retainedCharacterId = Guid.NewGuid();
        var addedCharacterId = Guid.NewGuid();
        var product = CreateInStock(
            [removedImage, retainedImage],
            [removedCharacterId, retainedCharacterId]);
        var retainedImageInstance = product.Images.Single(image => image.Id == retainedImage.Id);
        var retainedCharacterInstance = product.Characters.Single(
            link => link.CharacterId == retainedCharacterId);

        product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            product.InStockOffer!,
            [retainedImage, Image(Guid.NewGuid(), "added.webp")],
            [retainedCharacterId, addedCharacterId],
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Same(retainedImageInstance, product.Images[0]);
        Assert.Same(
            retainedCharacterInstance,
            product.Characters.Single(link => link.CharacterId == retainedCharacterId));
        Assert.Equal(2, product.Version);
    }

    [Fact]
    public void RetainedImageMetadataMismatchFailsAtomically()
    {
        var definition = Image(Guid.NewGuid(), "retained.webp");
        var product = CreateInStock([definition]);
        var retainedInstance = Assert.Single(product.Images);
        var before = Snapshot(product);

        AssertRule(
            ProductRule.ProductImageRetainedMetadataMismatch,
            () => product.UpdateDraftInStock(
                product.DisplayName,
                product.EnglishName,
                product.Description,
                product.Slug,
                product.ProductCategoryId,
                product.BrandId,
                product.UniverseId,
                product.InStockOffer!,
                [definition with { StorageKey = "products/example/tampered.webp" }],
                [],
                expectedVersion: 1,
                CreatedAtUtc.AddMinutes(1),
                "admin-2"));

        Assert.Equal(before, Snapshot(product));
        Assert.Same(retainedInstance, Assert.Single(product.Images));
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void SemanticNoOpIncludingWhitespaceAndCharacterOrderChangesNothing()
    {
        var images = new[]
        {
            Image(Guid.NewGuid(), "front.webp"),
            Image(Guid.NewGuid(), "back.webp"),
        };
        var characters = new[] { Guid.NewGuid(), Guid.NewGuid() };
        var product = CreateInStock(images, characters);
        var before = Snapshot(product);

        product.UpdateDraftInStock(
            $"  {product.DisplayName}  ",
            $"  {product.EnglishName}  ",
            $"  {product.Description}  ",
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            InStockOffer.Create(Money.Create(product.InStockOffer!.Price.Amount)),
            images.Select(image => image with { }).ToArray(),
            characters.Reverse().ToArray(),
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal(before, Snapshot(product));
    }

    [Fact]
    public void StaleOrInvalidCompleteReplacementLeavesEntireAggregateUnchanged()
    {
        var product = CreateInStock();
        var before = Snapshot(product);

        AssertRule(
            ProductRule.ProductConcurrencyVersionMismatch,
            () => UpdatePrice(product, expectedVersion: 2));
        Assert.Equal(before, Snapshot(product));

        AssertRule(
            ProductRule.ProductImageMetadataRequired,
            () => product.UpdateDraftInStock(
                product.DisplayName,
                product.EnglishName,
                product.Description,
                product.Slug,
                product.ProductCategoryId,
                product.BrandId,
                product.UniverseId,
                product.InStockOffer!,
                [Image(Guid.Empty, "front.webp")],
                [],
                expectedVersion: 1,
                CreatedAtUtc.AddMinutes(1),
                "admin-2"));
        Assert.Equal(before, Snapshot(product));
    }

    [Fact]
    public void ExhaustedVersionRejectsOtherwiseValidMutationWithoutOverflow()
    {
        var product = CreateInStock();
        typeof(Product).GetField("_version", BindingFlags.NonPublic | BindingFlags.Instance)!
            .SetValue(product, long.MaxValue);
        var before = Snapshot(product);

        AssertRule(
            ProductRule.ProductConcurrencyVersionExhausted,
            () => UpdatePrice(product, expectedVersion: long.MaxValue));

        Assert.Equal(before, Snapshot(product));
        Assert.Equal(long.MaxValue, product.Version);
    }

    [Fact]
    public void PublishAndArchiveRequireCurrentVersionAndAdvanceLifecycleOnce()
    {
        var product = CreateInStock([Image(Guid.NewGuid(), "front.webp")]);
        var beforeStalePublish = Snapshot(product);

        AssertRule(
            ProductRule.ProductConcurrencyVersionMismatch,
            () => product.Publish(expectedVersion: 2, CreatedAtUtc, "admin-2"));
        Assert.Equal(beforeStalePublish, Snapshot(product));

        product.Publish(expectedVersion: 1, CreatedAtUtc.AddMinutes(1), "admin-2");
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(2, product.Version);
        var beforeStaleArchive = Snapshot(product);

        AssertRule(
            ProductRule.ProductConcurrencyVersionMismatch,
            () => product.Archive(expectedVersion: 1, CreatedAtUtc.AddMinutes(2), "admin-3"));
        Assert.Equal(beforeStaleArchive, Snapshot(product));

        product.Archive(expectedVersion: 2, CreatedAtUtc.AddMinutes(2), "admin-3");
        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Equal(3, product.Version);
    }

    [Fact]
    public void PublishedInStockCanBeEditedButArchivedAndPreOrderCannotUseInStockUpdate()
    {
        var published = CreateInStock([Image(Guid.NewGuid(), "front.webp")]);
        published.Publish(expectedVersion: 1, CreatedAtUtc, "admin-1");
        UpdatePrice(published, expectedVersion: 2);
        Assert.Equal(ProductStatus.Published, published.Status);
        Assert.Equal(1990, published.InStockOffer!.Price.Amount);
        Assert.Equal(3, published.Version);

        var publishedBeforeInvalidMedia = Snapshot(published);
        AssertRule(
            ProductRule.ProductPublishRequiresImage,
            () => published.UpdateDraftInStock(
                published.DisplayName,
                published.EnglishName,
                published.Description,
                published.Slug,
                published.ProductCategoryId,
                published.BrandId,
                published.UniverseId,
                published.InStockOffer!,
                [],
                published.Characters.Select(link => link.CharacterId).ToArray(),
                published.Version,
                CreatedAtUtc.AddMinutes(4),
                "admin-4"));
        Assert.Equal(publishedBeforeInvalidMedia, Snapshot(published));

        published.Archive(expectedVersion: 3, CreatedAtUtc.AddMinutes(5), "admin-4");
        var archivedBefore = Snapshot(published);
        AssertRule(ProductRule.ProductEditsLocked, () => UpdatePrice(published, expectedVersion: 4));
        Assert.Equal(archivedBefore, Snapshot(published));

        var preOrder = CreatePreOrder();
        var preOrderBefore = Snapshot(preOrder);
        AssertRule(
            ProductRule.ProductInStockEditRequired,
            () => UpdatePrice(preOrder, expectedVersion: 1));
        Assert.Equal(preOrderBefore, Snapshot(preOrder));

        var mutationNames = typeof(Product)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Order()
            .ToArray();
        Assert.Equal(["Archive", "Publish", "UpdateDraftInStock", "UpdateDraftPreOrder"], mutationNames);
        Assert.False(
            typeof(Product).GetProperty(nameof(Product.SaleType))!.SetMethod?.IsPublic ?? false);
    }

    [Fact]
    public void PublishedPreOrderCanBeEditedWithoutChangingLifecycleStatus()
    {
        var image = Image(Guid.NewGuid(), "preorder.webp");
        var product = CreatePreOrder([image]);
        product.Publish(product.Version, CreatedAtUtc.AddMinutes(1), "publisher");
        var currentOffer = product.PreOrderOffer!;

        product.UpdateDraftPreOrder(
            "พรีออเดอร์แก้ไข",
            "Updated Pre Order",
            "รายละเอียดใหม่",
            "updated-pre-order",
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            PreOrderOffer.Create(
                Money.Create(2590),
                Money.Create(550),
                DateOnly.FromDateTime(currentOffer.CloseAtUtc.UtcDateTime),
                currentOffer.EstimatedArrival,
                currentOffer.TotalCapacity,
                currentOffer.MaxPerCustomer,
                CreatedAtUtc.AddMinutes(2),
                currentOffer.BalancePaymentDays),
            [image],
            [],
            product.Version,
            CreatedAtUtc.AddMinutes(2),
            "admin-2");

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal("พรีออเดอร์แก้ไข", product.DisplayName);
        Assert.Equal(2590, product.PreOrderOffer!.FullPrice.Amount);
        Assert.Equal(3, product.Version);

        product.UpdateDraftPreOrder(
                product.DisplayName,
                product.EnglishName,
                product.Description,
                product.Slug,
                product.ProductCategoryId,
                product.BrandId,
                product.UniverseId,
                PreOrderOffer.Create(
                    product.PreOrderOffer.FullPrice,
                    product.PreOrderOffer.DepositAmount,
                    DateOnly.FromDateTime(product.PreOrderOffer.CloseAtUtc.UtcDateTime),
                    product.PreOrderOffer.EstimatedArrival,
                    product.PreOrderOffer.TotalCapacity + 1,
                    product.PreOrderOffer.MaxPerCustomer,
                    CreatedAtUtc.AddMinutes(3),
                    product.PreOrderOffer.BalancePaymentDays),
                [image],
                [],
                product.Version,
                CreatedAtUtc.AddMinutes(3),
                "admin-3");
        Assert.Equal(currentOffer.TotalCapacity + 1, product.PreOrderOffer.TotalCapacity);

        var beforeCloseChange = Snapshot(product);
        AssertRule(
            ProductRule.ProductPublishedPreOrderCapacityLocked,
            () => product.UpdateDraftPreOrder(
                product.DisplayName,
                product.EnglishName,
                product.Description,
                product.Slug,
                product.ProductCategoryId,
                product.BrandId,
                product.UniverseId,
                PreOrderOffer.Create(
                    product.PreOrderOffer.FullPrice,
                    product.PreOrderOffer.DepositAmount,
                    new DateOnly(2026, 12, 30),
                    product.PreOrderOffer.EstimatedArrival,
                    product.PreOrderOffer.TotalCapacity,
                    product.PreOrderOffer.MaxPerCustomer,
                    CreatedAtUtc.AddMinutes(4),
                    product.PreOrderOffer.BalancePaymentDays),
                [image],
                [],
                product.Version,
                CreatedAtUtc.AddMinutes(4),
                "admin-4"));
        Assert.Equal(beforeCloseChange, Snapshot(product));
    }

    [Fact]
    public void DraftPreOrderWithImageCanPublishWhenApplicationCreatesCapacityAtomically()
    {
        var product = CreatePreOrder([Image(Guid.NewGuid(), "preorder.webp")]);
        product.Publish(product.Version, CreatedAtUtc.AddMinutes(1), "admin-2");

        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(2, product.Version);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), product.PublishedAtUtc);
    }

    [Fact]
    public void MaterializedPublishedPreOrderCannotArchiveBeforeCapacityPersistenceExists()
    {
        var product = CreatePreOrder([Image(Guid.NewGuid(), "preorder.webp")]);
        typeof(Product).GetProperty(nameof(Product.Status))!
            .SetValue(product, ProductStatus.Published);
        var before = Snapshot(product);

        AssertRule(
            ProductRule.ProductInStockLifecycleRequired,
            () => product.Archive(product.Version, CreatedAtUtc.AddMinutes(1), "admin-2"));

        Assert.Equal(before, Snapshot(product));
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(1, product.Version);
        Assert.Null(product.ArchivedAtUtc);
    }

    private static Product CreateInStock(
        IReadOnlyCollection<ProductImageDefinition>? images = null,
        IReadOnlyCollection<Guid>? characters = null) => Product.CreateInStock(
            ProductId,
            "กันดั้ม เอ็กเซีย",
            "Gundam Exia",
            "โมเดลสะสม",
            "gundam-exia",
            CategoryId,
            BrandId,
            UniverseId,
            InStockOffer.Create(Money.Create(1490)),
            images ?? [],
            characters ?? [],
            CreatedAtUtc,
            "admin-1");

    private static Product CreatePreOrder(
        IReadOnlyCollection<ProductImageDefinition>? images = null,
        IReadOnlyCollection<Guid>? characters = null) => Product.CreatePreOrder(
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
            images ?? [],
            characters ?? [],
            CreatedAtUtc,
            "admin-1");

    private static ProductImageDefinition Image(Guid imageId, string fileName) => new(
        imageId,
        $"products/example/{fileName}",
        $"/media/products/example/{fileName}",
        $"สินค้า {fileName}");

    private static void UpdatePrice(Product product, long expectedVersion) =>
        product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            InStockOffer.Create(Money.Create(1990)),
            product.Images.Select(image => new ProductImageDefinition(
                image.Id,
                image.StorageKey,
                image.PublicRelativeUrl,
                image.AltText)).ToArray(),
            product.Characters.Select(link => link.CharacterId).ToArray(),
            expectedVersion,
            CreatedAtUtc.AddMinutes(3),
            "admin-3");

    private static object Snapshot(Product product) => new
    {
        product.DisplayName,
        product.NormalizedDisplayName,
        product.EnglishName,
        product.NormalizedEnglishName,
        product.Description,
        product.Slug,
        product.ProductCategoryId,
        product.BrandId,
        product.UniverseId,
        Price = product.InStockOffer?.Price.Amount,
        Images = string.Join('|', product.Images.Select(image =>
            $"{image.Id}:{image.StorageKey}:{image.PublicRelativeUrl}:{image.AltText}:{image.SortOrder}:{image.IsPrimary}")),
        Characters = string.Join(',', product.Characters.Select(link => link.CharacterId)),
        product.Status,
        product.UpdatedAtUtc,
        product.UpdatedBy,
        product.PublishedAtUtc,
        product.PublishedBy,
        product.ArchivedAtUtc,
        product.ArchivedBy,
        product.Version,
    };

    private static void AssertRule(ProductRule expectedRule, Action action)
    {
        var exception = Assert.Throws<ProductRuleException>(action);
        Assert.Equal(expectedRule, exception.Rule);
    }
}
