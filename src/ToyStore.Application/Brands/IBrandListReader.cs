using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands;

public interface IBrandListReader
{
    Task<BrandListReadPage> ReadAsync(
        BrandListReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record BrandListReadRequest(
    string? NormalizedSearch,
    CatalogReferenceStatus? Status,
    int PageNumber,
    int PageSize);

public sealed record BrandListReadPage(
    IReadOnlyList<BrandListReadItem> Items,
    int EffectivePageNumber,
    int TotalCount);

public sealed record BrandListReadItem(
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
