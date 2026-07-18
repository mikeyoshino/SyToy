using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.ManageProducts;

public interface IProductManagementReader
{
    Task<ProductManagementReadPage> ReadAsync(
        ProductManagementReadRequest request,
        CancellationToken cancellationToken);
}

public sealed record ProductManagementReadRequest(
    string? NormalizedSearch,
    ProductStatus? Status,
    Guid? ProductCategoryId,
    Guid? BrandId,
    Guid? UniverseId,
    int PageNumber,
    int PageSize);

public sealed record ProductManagementReadPage(
    IReadOnlyList<ProductManagementReadItem> Items,
    IReadOnlyList<ProductManagementReferenceOption> Categories,
    IReadOnlyList<ProductManagementReferenceOption> BrandFilterOptions,
    IReadOnlyList<ProductManagementReferenceOption> UniverseFilterOptions,
    IReadOnlyList<ProductManagementReferenceOption> BrandEditorOptions,
    IReadOnlyList<ProductManagementReferenceOption> UniverseEditorOptions,
    int EffectivePageNumber,
    int TotalCount);

public sealed record ProductManagementReadItem(
    Guid Id,
    string DisplayName,
    string EnglishName,
    string Description,
    string Slug,
    Guid ProductCategoryId,
    string ProductCategoryCode,
    Guid BrandId,
    string BrandName,
    Guid UniverseId,
    string UniverseName,
    decimal Price,
    ProductStatus Status,
    long Version,
    int OnHandQuantity,
    int ReservableQuantity,
    IReadOnlyList<ProductManagementImage> Images,
    IReadOnlyList<ProductManagementCharacter> Characters,
    DateTimeOffset UpdatedAtUtc)
{
    public ProductManagementSaleType SaleType { get; init; }
    public decimal? FullPrice { get; init; }
    public decimal? DepositAmount { get; init; }
    public DateTimeOffset? CloseAtUtc { get; init; }
    public int? EstimatedArrivalMonth { get; init; }
    public int? EstimatedArrivalYear { get; init; }
    public int? TotalCapacity { get; init; }
    public int? MaxPerCustomer { get; init; }
    public int? BalancePaymentDays { get; init; }
}

public sealed record ProductManagementImage(
    Guid Id,
    string PublicRelativeUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);

public sealed record ProductManagementCharacter(Guid Id, Guid UniverseId, string Name);

public sealed record ProductManagementReferenceOption(
    Guid Id,
    string Name,
    string? Code = null,
    bool IsActive = true);
