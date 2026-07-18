using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Storefront.Catalog;

public sealed class ListStorefrontProductsHandler(IStorefrontCatalogReader reader, TimeProvider timeProvider)
    : IRequestHandler<ListStorefrontProductsQuery, Result<StorefrontCatalogPage>>
{
    public async Task<Result<StorefrontCatalogPage>> Handle(ListStorefrontProductsQuery request, CancellationToken cancellationToken)
    {
        var read = await reader.ListAsync(new StorefrontCatalogReadRequest(
            string.IsNullOrWhiteSpace(request.Search) ? null : CatalogNameNormalizer.Normalize(request.Search),
            request.SaleType, request.ProductCategoryId, request.BrandId,
            string.IsNullOrWhiteSpace(request.BrandSlug) ? null : request.BrandSlug.Trim().ToLowerInvariant(),
            request.CharacterId, request.UniverseId, request.MinimumPrice, request.MaximumPrice,
            request.Page, request.PageSize, timeProvider.GetUtcNow().ToUniversalTime()), cancellationToken);
        return Result<StorefrontCatalogPage>.Success(new StorefrontCatalogPage(
            read.Items, read.Categories, read.Brands, read.Characters, read.Universes,
            read.BrandDisplayName, read.EffectivePageNumber, request.PageSize, read.TotalCount));
    }
}
