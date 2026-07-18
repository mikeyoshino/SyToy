using System.Globalization;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class CatalogValueTests
{
    [Fact]
    public void NameNormalizerRejectsBlankNameWithStableRule()
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(
            () => CatalogNameNormalizer.Normalize("\u2003 \t"));

        Assert.Equal(CatalogReferenceRule.NameRequired, exception.Rule);
    }

    [Fact]
    public void NameNormalizerAppliesFormKcUnicodeWhitespaceAndInvariantUppercase()
    {
        var normalized = CatalogNameNormalizer.Normalize(" \uFF27undam\u2003\t  exia ");

        Assert.Equal("GUNDAM EXIA", normalized);
        Assert.Equal(
            CatalogNameNormalizer.Normalize("Cafe\u0301"),
            CatalogNameNormalizer.Normalize("Caf\u00E9"));
    }

    [Fact]
    public void NameNormalizerIsIndependentOfCurrentCulture()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");

            Assert.Equal("MINI", CatalogNameNormalizer.Normalize("mini"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Theory]
    [InlineData("art-toy")]
    [InlineData("gundam")]
    [InlineData("gundam-rx-78-2")]
    [InlineData("a1")]
    public void GeneratedSlugAcceptsOnlyLowercaseAsciiSegments(string value)
    {
        var slug = CatalogSlug.Create(value);

        Assert.Equal(value, slug.Value);
        Assert.Equal(value, slug.ToString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("Gundam")]
    [InlineData("-gundam")]
    [InlineData("gundam-")]
    [InlineData("gundam--rx")]
    [InlineData("gundam rx")]
    [InlineData("gundam\n")]
    [InlineData("gundam\r")]
    [InlineData("gundam\0")]
    [InlineData("กันดั้ม")]
    public void GeneratedSlugRejectsInvalidValuesWithStableRule(string value)
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(
            () => CatalogSlug.Create(value));

        Assert.Equal(CatalogReferenceRule.SlugInvalid, exception.Rule);
    }

    [Fact]
    public void CatalogSlugHasStructuralEqualityAndStableHashCode()
    {
        var first = CatalogSlug.Create("marvel-legends");
        var second = CatalogSlug.Create("marvel-legends");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void ProductBrandAndUniverseShareTheSameGeneratedSlugGrammar()
    {
        var valid = CatalogSlug.Create("shared-slug-2");
        var createdAtUtc = new DateTimeOffset(2026, 7, 17, 8, 0, 0, TimeSpan.Zero);

        var product = Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้า",
            "Shared Slug",
            "รายละเอียด",
            valid.Value,
            CatalogSeedIds.ArtToyCategory,
            Guid.NewGuid(),
            CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)),
            createdAtUtc,
            "admin-1");
        var brand = Brand.Create(
            Guid.NewGuid(),
            "แบรนด์",
            "Brand",
            valid,
            createdAtUtc,
            "admin-1");
        var universe = Universe.Create(
            Guid.NewGuid(),
            "จักรวาล",
            "Universe",
            valid,
            createdAtUtc,
            "admin-1");

        Assert.Equal(valid.Value, product.Slug);
        Assert.Equal(valid, brand.Slug);
        Assert.Equal(valid, universe.Slug);
        var invalidProduct = Assert.Throws<ProductRuleException>(() => Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้า",
            "Bad Slug",
            "รายละเอียด",
            "shared-slug\n",
            CatalogSeedIds.ArtToyCategory,
            Guid.NewGuid(),
            CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)),
            createdAtUtc,
            "admin-1"));
        Assert.Equal(ProductRule.ProductSlugInvalid, invalidProduct.Rule);
        Assert.Throws<CatalogReferenceRuleException>(() => CatalogSlug.Create("shared-slug\n"));
    }

    [Theory]
    [InlineData("storage", CatalogReferenceRule.MediaStorageKeyRequired)]
    [InlineData("url", CatalogReferenceRule.MediaRelativeUrlRequired)]
    [InlineData("alt", CatalogReferenceRule.MediaAltTextRequired)]
    public void MediaReferenceRejectsBlankMetadata(string field, CatalogReferenceRule expectedRule)
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(() =>
            CatalogMediaReference.Create(
                field == "storage" ? " " : "brands/bandai.webp",
                field == "url" ? " " : "/media/brands/bandai.webp",
                field == "alt" ? " " : "โลโก้ Bandai"));

        Assert.Equal(expectedRule, exception.Rule);
    }

    [Fact]
    public void MediaReferenceHasStructuralEqualityAndStableHashCode()
    {
        var first = CatalogMediaReference.Create(
            "brands/bandai.webp",
            "/media/brands/bandai.webp",
            "โลโก้ Bandai");
        var second = CatalogMediaReference.Create(
            "brands/bandai.webp",
            "/media/brands/bandai.webp",
            "โลโก้ Bandai");

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }
}
