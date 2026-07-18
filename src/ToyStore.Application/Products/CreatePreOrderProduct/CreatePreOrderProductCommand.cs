using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.CreatePreOrderProduct;

public sealed record CreatePreOrderProductCommand(
    string DisplayName,
    string EnglishName,
    string Description,
    Guid ProductCategoryId,
    Guid BrandId,
    Guid UniverseId,
    IReadOnlyList<Guid> CharacterIds,
    decimal FullPrice,
    decimal DepositAmount,
    DateOnly CloseDate,
    int EstimatedArrivalMonth,
    int EstimatedArrivalYear,
    int TotalCapacity,
    int MaxPerCustomer,
    int BalancePaymentDays,
    IReadOnlyList<ProductMediaPlanSlot> Images,
    string? ModelScale = null)
    : AuthorizedProductMutationRequest<Result<ProductMutationResult>>
{
    public override Result<ProductMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductMutationResult>.Failure(requestError, validationFailures);
}
