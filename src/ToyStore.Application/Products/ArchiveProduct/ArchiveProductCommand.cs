using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Products.ArchiveProduct;

public sealed record ArchiveProductCommand(Guid Id, long ExpectedVersion)
    : AuthorizedProductMutationRequest<Result<ProductMutationResult>>
{
    public override Result<ProductMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<ProductMutationResult>.Failure(requestError, validationFailures);
}
