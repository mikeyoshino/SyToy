using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.ListBrands;

public sealed record BrandListItem(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Slug,
    string? ImagePublicRelativeUrl,
    string? ImageAltText,
    CatalogReferenceStatus Status,
    bool CanBeUsedByPublishedProduct,
    long Version,
    int ProductReferenceCount,
    DateTimeOffset UpdatedAtUtc);
