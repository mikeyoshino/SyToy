namespace ToyStore.Application.Storefront.Catalog;

public interface IStorefrontCatalogReader
{
    Task<StorefrontCatalogReadPage> ListAsync(
        StorefrontCatalogReadRequest request,
        CancellationToken cancellationToken);

    Task<StorefrontProductDetail?> FindBySlugAsync(
        string slug,
        DateTimeOffset nowUtc,
        CancellationToken cancellationToken);
}

public sealed record StorefrontCatalogReadRequest(
    string? NormalizedSearch,
    StorefrontSaleTypeFilter SaleType,
    Guid? ProductCategoryId,
    Guid? BrandId,
    string? BrandSlug,
    Guid? CharacterId,
    Guid? UniverseId,
    decimal? MinimumPrice,
    decimal? MaximumPrice,
    int PageNumber,
    int PageSize,
    DateTimeOffset NowUtc);

public sealed record StorefrontCatalogReadPage(
    IReadOnlyList<StorefrontProductCard> Items,
    IReadOnlyList<StorefrontFilterOption> Categories,
    IReadOnlyList<StorefrontFilterOption> Brands,
    IReadOnlyList<StorefrontFilterOption> Characters,
    IReadOnlyList<StorefrontFilterOption> Universes,
    string? BrandDisplayName,
    int EffectivePageNumber,
    int TotalCount);

public enum StorefrontSaleType
{
    InStock,
    PreOrder,
}

public enum StorefrontOfferState
{
    InStockAvailable,
    InStockOutOfStock,
    PreOrderOpen,
    PreOrderClosed,
    PreOrderFull,
}

public sealed record StorefrontProductCard(
    Guid Id,
    string DisplayName,
    string Slug,
    string BrandName,
    string CategoryName,
    StorefrontSaleType SaleType,
    StorefrontOfferState OfferState,
    decimal Price,
    decimal? DepositAmount,
    int AvailableQuantity,
    string PrimaryImageUrl,
    string PrimaryImageAltText,
    string? ModelScale = null)
{
    public bool IsAvailable => OfferState is StorefrontOfferState.InStockAvailable
        or StorefrontOfferState.PreOrderOpen;
}

public sealed record StorefrontProductDetail(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Description,
    string Slug,
    string BrandName,
    string BrandSlug,
    string CategoryName,
    string UniverseName,
    IReadOnlyList<string> Characters,
    StorefrontSaleType SaleType,
    StorefrontOfferState OfferState,
    decimal Price,
    decimal? DepositAmount,
    decimal? BalanceAmount,
    int AvailableQuantity,
    DateTimeOffset? PreOrderCloseAtUtc,
    int? EstimatedArrivalMonth,
    int? EstimatedArrivalYear,
    int? MaxPerCustomer,
    int? BalancePaymentDays,
    IReadOnlyList<StorefrontProductImage> Images,
    string? ModelScale = null);

public sealed record StorefrontProductImage(string Url, string AltText, int SortOrder, bool IsPrimary);

public sealed record StorefrontFilterOption(Guid Id, string Label, string? Slug = null);
