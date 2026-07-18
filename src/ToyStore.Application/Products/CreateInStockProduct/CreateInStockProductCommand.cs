using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.CreateInStockProduct;

public sealed record CreateInStockProductCommand(
    string DisplayName,
    string EnglishName,
    string Description,
    Guid ProductCategoryId,
    Guid BrandId,
    Guid UniverseId,
    IReadOnlyList<Guid> CharacterIds,
    decimal Price,
    int InitialStock,
    IReadOnlyList<ProductMediaPlanSlot> Images)
    : AuthorizedProductMutationRequest<Result<ProductMutationResult>>
{
    public override Result<ProductMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductMutationResult>.Failure(requestError, validationFailures);
}
