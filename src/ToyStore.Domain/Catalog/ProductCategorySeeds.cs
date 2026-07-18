namespace ToyStore.Domain.Catalog;

public static class ProductCategorySeeds
{
    public static IReadOnlyList<ProductCategory> All => Validate(
    [
        ProductCategory.Create(CatalogSeedIds.ArtToyCategory, "ArtToy"),
        ProductCategory.Create(CatalogSeedIds.GundamCategory, "Gundam"),
    ]);

    internal static IReadOnlyList<ProductCategory> Validate(
        IEnumerable<ProductCategory> definitions)
    {
        var snapshot = definitions.ToArray();
        if (snapshot.Select(definition => definition.Id).Distinct().Count() != snapshot.Length)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SeedIdentityDuplicate);
        }

        if (snapshot.Select(definition => definition.Code)
            .Distinct(StringComparer.Ordinal)
            .Count() != snapshot.Length)
        {
            throw new CatalogReferenceRuleException(CatalogReferenceRule.SeedCodeDuplicate);
        }

        return Array.AsReadOnly(snapshot);
    }
}
