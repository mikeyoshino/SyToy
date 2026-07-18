using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class CatalogSlugGeneratorTests
{
    [Theory]
    [InlineData("Gundam Exia", "gundam-exia")]
    [InlineData("  TOY---Story / 2  ", "toy-story-2")]
    [InlineData("ＴＯＹ １２", "toy-12")]
    [InlineData("Art___Toy", "art-toy")]
    public void GenerateBaseNormalizesAsciiWordsAndSeparatorRuns(
        string englishName,
        string expected)
    {
        Assert.Equal(expected, CatalogSlugGenerator.GenerateBase(englishName).Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("กันดั้ม")]
    [InlineData("---")]
    public void GenerateBaseRejectsNamesWithoutAsciiContent(string englishName)
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(
            () => CatalogSlugGenerator.GenerateBase(englishName));

        Assert.Equal(CatalogReferenceRule.SlugCannotBeGenerated, exception.Rule);
    }
}
