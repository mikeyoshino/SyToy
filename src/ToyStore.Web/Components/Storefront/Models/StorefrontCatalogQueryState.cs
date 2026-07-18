namespace ToyStore.Web.Components.Storefront.Models;

public sealed record StorefrontCatalogQueryState(
    string? Search = null,
    string SaleType = "all",
    Guid? CategoryId = null,
    Guid? BrandId = null,
    Guid? CharacterId = null,
    Guid? UniverseId = null,
    decimal? MinimumPrice = null,
    decimal? MaximumPrice = null,
    int Page = 1);
