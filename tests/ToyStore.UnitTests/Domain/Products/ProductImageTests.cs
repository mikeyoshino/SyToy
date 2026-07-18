using System.Collections;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Products;

public sealed class ProductImageTests
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("id")]
    [InlineData("storageKey")]
    [InlineData("publicRelativeUrl")]
    [InlineData("altText")]
    public void CompleteFactoryRejectsMissingDurableMetadata(string field)
    {
        var definition = new ProductImageDefinition(
            field == "id" ? Guid.Empty : Guid.NewGuid(),
            field == "storageKey" ? " " : "products/example/front.webp",
            field == "publicRelativeUrl" ? " " : "/media/products/example/front.webp",
            field == "altText" ? " " : "กันดั้ม เอ็กเซีย มุมด้านหน้า");

        var exception = Assert.Throws<ProductRuleException>(() => CreateProduct([definition]));

        Assert.Equal(ProductRule.ProductImageMetadataRequired, exception.Rule);
    }

    [Fact]
    public void CompleteFactoryPreservesOrderAndDerivesOnlyFirstAsPrimaryAtVersionOne()
    {
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        var product = CreateProduct([
            Image(firstId, "front.webp"),
            Image(secondId, "back.webp"),
        ]);

        Assert.Collection(
            product.Images,
            image =>
            {
                Assert.Equal(firstId, image.Id);
                Assert.Equal(0, image.SortOrder);
                Assert.True(image.IsPrimary);
            },
            image =>
            {
                Assert.Equal(secondId, image.Id);
                Assert.Equal(1, image.SortOrder);
                Assert.False(image.IsPrimary);
            });
        Assert.Equal(1, product.Version);
        Assert.Throws<NotSupportedException>(
            () => ((IList)product.Images).Add(product.Images[0]));
    }

    [Theory]
    [InlineData("id", ProductRule.ProductImageDuplicateId)]
    [InlineData("storage", ProductRule.ProductImageDuplicateStorageKey)]
    public void CompleteReplacementRejectsDuplicateImageEvidenceAtomically(
        string duplicate,
        ProductRule expectedRule)
    {
        var product = CreateProduct();
        var sharedId = Guid.NewGuid();
        var first = Image(sharedId, "front.webp");
        var second = Image(
            duplicate == "id" ? sharedId : Guid.NewGuid(),
            duplicate == "storage" ? "front.webp" : "back.webp");
        var before = Snapshot(product);

        var exception = Assert.Throws<ProductRuleException>(() => UpdateImages(
            product,
            [first, second],
            expectedVersion: 1,
            CreatedAtUtc,
            "admin-2"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(before, Snapshot(product));
    }

    [Fact]
    public void ProductAcceptsAtMostEightImagesWithoutPartialMutation()
    {
        var product = CreateProduct();
        var images = Enumerable.Range(0, Product.MaximumImageCount + 1)
            .Select(index => Image(Guid.NewGuid(), $"image-{index}.webp"))
            .ToArray();
        var before = Snapshot(product);

        var exception = Assert.Throws<ProductRuleException>(() => UpdateImages(
            product,
            images,
            expectedVersion: 1,
            CreatedAtUtc,
            "admin-2"));

        Assert.Equal(ProductRule.ProductImageLimitExceeded, exception.Rule);
        Assert.Equal(before, Snapshot(product));
    }

    [Fact]
    public void AtomicReplacementReordersAndRemovesImagesWithOneVersionIncrement()
    {
        var first = Image(Guid.NewGuid(), "front.webp");
        var second = Image(Guid.NewGuid(), "back.webp");
        var third = Image(Guid.NewGuid(), "side.webp");
        var product = CreateProduct([first, second, third]);

        UpdateImages(
            product,
            [third, first],
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal([third.Id, first.Id], product.Images.Select(image => image.Id));
        Assert.Equal([0, 1], product.Images.Select(image => image.SortOrder));
        Assert.True(product.Images[0].IsPrimary);
        Assert.False(product.Images[1].IsPrimary);
        Assert.Equal(2, product.Version);
        Assert.Equal(CreatedAtUtc.AddMinutes(1), product.UpdatedAtUtc);
        Assert.Equal("admin-2", product.UpdatedBy);
    }

    [Fact]
    public void CompleteReplacementSnapshotsChangingInputsExactlyOnceBeforeMutation()
    {
        var first = Image(Guid.NewGuid(), "front.webp");
        var second = Image(Guid.NewGuid(), "back.webp");
        var product = CreateProduct([first, second]);
        var changingImages = new ChangingReadOnlyCollection<ProductImageDefinition>(
            [second, first],
            [second, Image(Guid.NewGuid(), "unexpected.webp")]);

        UpdateImages(
            product,
            changingImages,
            expectedVersion: 1,
            CreatedAtUtc.AddMinutes(1),
            "admin-2");

        Assert.Equal(1, changingImages.EnumerationCount);
        Assert.Equal([second.Id, first.Id], product.Images.Select(image => image.Id));
        Assert.Equal(2, product.Version);
    }

    [Theory]
    [InlineData("nonUtc", ProductRule.UtcInstantRequired)]
    [InlineData("backwards", ProductRule.ProductAuditTimeWentBackwards)]
    [InlineData("actor", ProductRule.ProductActorRequired)]
    public void CompleteReplacementRejectsInvalidAuditAtomically(
        string invalidField,
        ProductRule expectedRule)
    {
        var product = CreateProduct();
        var before = Snapshot(product);
        var changedAtUtc = invalidField switch
        {
            "nonUtc" => CreatedAtUtc.ToOffset(TimeSpan.FromHours(7)),
            "backwards" => CreatedAtUtc.AddTicks(-1),
            _ => CreatedAtUtc,
        };

        var exception = Assert.Throws<ProductRuleException>(() => UpdateImages(
            product,
            [Image(Guid.NewGuid(), "front.webp")],
            expectedVersion: 1,
            changedAtUtc,
            invalidField == "actor" ? " " : "admin-2"));

        Assert.Equal(expectedRule, exception.Rule);
        Assert.Equal(before, Snapshot(product));
    }

    private static Product CreateProduct(
        IReadOnlyCollection<ProductImageDefinition>? images = null) =>
        Product.CreateInStock(
            Guid.NewGuid(),
            "กันดั้ม เอ็กเซีย",
            "Gundam Exia",
            "โมเดลสะสม",
            "gundam-exia",
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            InStockOffer.Create(Money.Create(1490)),
            images ?? [],
            [],
            CreatedAtUtc,
            "admin-1");

    private static ProductImageDefinition Image(Guid imageId, string fileName) => new(
        imageId,
        $"products/example/{fileName}",
        $"/media/products/example/{fileName}",
        $"กันดั้ม เอ็กเซีย {fileName}");

    private static void UpdateImages(
        Product product,
        IReadOnlyCollection<ProductImageDefinition> images,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor) => product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            product.InStockOffer!,
            images,
            product.Characters.Select(link => link.CharacterId).ToArray(),
            expectedVersion,
            changedAtUtc,
            actor);

    private static object Snapshot(Product product) => new
    {
        Images = string.Join('|', product.Images.Select(image =>
            $"{image.Id}:{image.StorageKey}:{image.PublicRelativeUrl}:{image.AltText}:{image.SortOrder}:{image.IsPrimary}")),
        product.UpdatedAtUtc,
        product.UpdatedBy,
        product.Version,
    };

    private sealed class ChangingReadOnlyCollection<T>(
        IReadOnlyList<T> firstEnumeration,
        IReadOnlyList<T> laterEnumerations) : IReadOnlyCollection<T>
    {
        public int Count => firstEnumeration.Count;

        public int EnumerationCount { get; private set; }

        public IEnumerator<T> GetEnumerator()
        {
            EnumerationCount++;
            return (EnumerationCount == 1 ? firstEnumeration : laterEnumerations).GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
