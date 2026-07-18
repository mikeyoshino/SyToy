using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.ListUniverses;

public sealed record UniverseListItem(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Slug,
    string? LogoPublicRelativeUrl,
    string? LogoAltText,
    CatalogReferenceStatus Status,
    bool CanBeUsedByPublishedProduct,
    long Version,
    int ProductReferenceCount,
    int CharacterReferenceCount,
    DateTimeOffset UpdatedAtUtc);
