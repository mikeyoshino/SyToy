using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.PublishProduct;

public sealed record PublishProductCommand(Guid Id, long ExpectedVersion)
    : AuthorizedProductMutationRequest<Result<ProductMutationResult>>
{
    public override Result<ProductMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductMutationResult>.Failure(requestError, validationFailures);
}
