using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.UpdateDraftInStockProduct;

public sealed record UpdateDraftInStockProductCommand(
    Guid Id,
    long ExpectedVersion,
    string DisplayName,
    string EnglishName,
    string Description,
    Guid ProductCategoryId,
    Guid BrandId,
    Guid UniverseId,
    IReadOnlyList<Guid> CharacterIds,
    decimal Price,
    IReadOnlyList<ProductMediaPlanSlot> Images)
    : AuthorizedProductMutationRequest<Result<ProductMutationResult>>
{
    public override Result<ProductMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductMutationResult>.Failure(requestError, validationFailures);
}
