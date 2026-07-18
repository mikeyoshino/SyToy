using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.ListUniverses;

public sealed class ListUniversesHandler(IUniverseListReader reader)
    : IRequestHandler<ListUniversesQuery, Result<PagedResult<UniverseListItem>>>
{
    public async Task<Result<PagedResult<UniverseListItem>>> Handle(
        ListUniversesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var readPage = await reader.ReadAsync(
            new UniverseListReadRequest(
                NormalizeSearch(request.Search),
                MapStatus(request.Status),
                request.Page,
                request.PageSize),
            cancellationToken);
        var items = readPage.Items
            .Select(item => new UniverseListItem(
                item.Id,
                item.DisplayName,
                item.EnglishName,
                item.Slug,
                item.LogoPublicRelativeUrl,
                item.LogoAltText,
                item.Status,
                item.CanBeUsedByPublishedProduct,
                item.Version,
                item.ProductReferenceCount,
                item.CharacterReferenceCount,
                item.UpdatedAtUtc))
            .ToArray();
        return Result<PagedResult<UniverseListItem>>.Success(
            new PagedResult<UniverseListItem>(
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
