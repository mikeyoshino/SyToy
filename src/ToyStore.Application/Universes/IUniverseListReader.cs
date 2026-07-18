using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes;

public interface IUniverseListReader
{
    Task<UniverseListReadPage> ReadAsync(
        UniverseListReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record UniverseListReadRequest(
    string? NormalizedSearch,
    CatalogReferenceStatus? Status,
    int PageNumber,
    int PageSize);

public sealed record UniverseListReadPage(
    IReadOnlyList<UniverseListReadItem> Items,
    int EffectivePageNumber,
    int TotalCount);

public sealed record UniverseListReadItem(
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
