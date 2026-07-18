using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.ListBrands;

public sealed class ListBrandsHandler(IBrandListReader reader)
    : IRequestHandler<ListBrandsQuery, Result<PagedResult<BrandListItem>>>
{
    public async Task<Result<PagedResult<BrandListItem>>> Handle(
        ListBrandsQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readPage = await reader.ReadAsync(
            new BrandListReadRequest(
                NormalizeSearch(request.Search),
                MapStatus(request.Status),
                request.Page,
                request.PageSize),
            cancellationToken);
        var items = readPage.Items
            .Select(item => new BrandListItem(
                item.Id,
                item.DisplayName,
                item.EnglishName,
                item.Slug,
                item.ImagePublicRelativeUrl,
                item.ImageAltText,
                item.Status,
                item.CanBeUsedByPublishedProduct,
                item.Version,
                item.ProductReferenceCount,
                item.UpdatedAtUtc))
            .ToArray();
        return Result<PagedResult<BrandListItem>>.Success(
            new PagedResult<BrandListItem>(
                items,
                readPage.EffectivePageNumber,
                request.PageSize,
                readPage.TotalCount));
    }

    private static string? NormalizeSearch(string? search) =>
        string.IsNullOrWhiteSpace(search)
            ? null
            : CatalogNameNormalizer.Normalize(search);

    private static CatalogReferenceStatus? MapStatus(CatalogReferenceListStatus status) =>
        status switch
        {
            CatalogReferenceListStatus.Active => CatalogReferenceStatus.Active,
            CatalogReferenceListStatus.Archived => CatalogReferenceStatus.Archived,
            CatalogReferenceListStatus.All => null,
            _ => throw new ArgumentOutOfRangeException(nameof(status)),
        };
}
