using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Storefront.Catalog;

public enum StorefrontSaleTypeFilter { All, InStock, PreOrder }

public sealed record ListStorefrontProductsQuery(
    string? Search = null,
    StorefrontSaleTypeFilter SaleType = StorefrontSaleTypeFilter.All,
    Guid? ProductCategoryId = null,
    Guid? BrandId = null,
    string? BrandSlug = null,
    Guid? CharacterId = null,
    Guid? UniverseId = null,
    decimal? MinimumPrice = null,
    decimal? MaximumPrice = null,
    int Page = 1,
    int PageSize = 24) : IRequest<Result<StorefrontCatalogPage>>;

public sealed record StorefrontCatalogPage(
    IReadOnlyList<StorefrontProductCard> Items,
    IReadOnlyList<StorefrontFilterOption> Categories,
    IReadOnlyList<StorefrontFilterOption> Brands,
    IReadOnlyList<StorefrontFilterOption> Characters,
    IReadOnlyList<StorefrontFilterOption> Universes,
    string? BrandDisplayName,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0 ? 0 : (int)(((long)TotalCount + PageSize - 1) / PageSize);
}
