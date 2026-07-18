using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands;

public sealed record BrandMutationResult(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Slug,
    long Version,
    CatalogReferenceStatus Status)
{
    public static BrandMutationResult From(Brand brand)
    {
        ArgumentNullException.ThrowIfNull(brand);
        return new BrandMutationResult(
            brand.Id,
            brand.DisplayName,
            brand.EnglishName,
            brand.Slug.Value,
            brand.Version,
            brand.Status);
    }

    public static BrandMutationResult From(BrandMutationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new BrandMutationResult(
            evidence.Id,
            evidence.DisplayName,
            evidence.EnglishName,
            evidence.Slug,
            evidence.IntendedVersion,
            evidence.Status);
    }
}
