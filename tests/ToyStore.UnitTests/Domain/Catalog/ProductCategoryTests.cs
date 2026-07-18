using System.Reflection;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Domain.Catalog;

public sealed class ProductCategoryTests
{
    [Fact]
    public void SeedsContainExactlyTheApprovedLiteralIdentitiesAndCodes()
    {
        var definitions = ProductCategorySeeds.All;

        Assert.Equal(2, definitions.Count);
        Assert.Collection(
            definitions,
            artToy =>
            {
                Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000001"), artToy.Id);
                Assert.Equal("ArtToy", artToy.Code);
            },
            gundam =>
            {
                Assert.Equal(Guid.Parse("10000000-0000-0000-0000-000000000002"), gundam.Id);
                Assert.Equal("Gundam", gundam.Code);
            });
    }

    [Fact]
    public void SeedsReturnFreshReadOnlyCollectionsWithoutCategoryManagementSurface()
    {
        var first = ProductCategorySeeds.All;
        var second = ProductCategorySeeds.All;

        Assert.NotSame(first, second);
        Assert.IsAssignableFrom<IReadOnlyList<ProductCategory>>(first);
        Assert.Empty(typeof(ProductCategory).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.All(
            typeof(ProductCategory).GetProperties(),
            property => Assert.False(property.SetMethod?.IsPublic ?? false));
        Assert.Equal(["Code", "Id"], typeof(ProductCategory).GetProperties()
            .Select(property => property.Name)
            .Order()
            .ToArray());
        Assert.DoesNotContain(
            typeof(ProductCategory).GetMethods(
                BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly),
            method => !method.IsSpecialName);
    }

    [Fact]
    public void SeedSetRejectsDuplicateIdentity()
    {
        var duplicateId = Guid.Parse("10000000-0000-0000-0000-000000000099");

        var exception = Assert.Throws<CatalogReferenceRuleException>(() =>
            ProductCategorySeeds.Validate(
            [
                ProductCategory.Create(duplicateId, "First"),
                ProductCategory.Create(duplicateId, "Second"),
            ]));

        Assert.Equal(CatalogReferenceRule.SeedIdentityDuplicate, exception.Rule);
    }

    [Fact]
    public void SeedSetRejectsDuplicateCode()
    {
        var exception = Assert.Throws<CatalogReferenceRuleException>(() =>
            ProductCategorySeeds.Validate(
            [
                ProductCategory.Create(Guid.NewGuid(), "ArtToy"),
                ProductCategory.Create(Guid.NewGuid(), "ArtToy"),
            ]));

        Assert.Equal(CatalogReferenceRule.SeedCodeDuplicate, exception.Rule);
    }
}
