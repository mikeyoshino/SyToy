using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.ManageProducts;

public sealed record ManageProductsQuery(
    string? Search = null,
    ProductManagementStatus? Status = null,
    Guid? ProductCategoryId = null,
    Guid? BrandId = null,
    Guid? UniverseId = null,
    int Page = 1,
    int PageSize = 20)
    : AuthorizedResultRequest<Result<ProductManagementPage>>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public override Result<ProductManagementPage> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductManagementPage>.Failure(requestError, validationFailures);
}

public enum ProductManagementStatus { Draft, Published, Archived }
public enum ProductManagementSaleType { InStock, PreOrder }

public sealed record ProductManagementPage(
    IReadOnlyList<ProductManagementItem> Items,
    IReadOnlyList<ProductManagementReferenceOption> Categories,
    IReadOnlyList<ProductManagementReferenceOption> BrandFilterOptions,
    IReadOnlyList<ProductManagementReferenceOption> UniverseFilterOptions,
    IReadOnlyList<ProductManagementReferenceOption> BrandEditorOptions,
    IReadOnlyList<ProductManagementReferenceOption> UniverseEditorOptions,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => TotalCount == 0
        ? 0
        : (int)(((long)TotalCount + PageSize - 1) / PageSize);
}

public sealed record ProductManagementItem(
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
    ProductManagementStatus Status,
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
