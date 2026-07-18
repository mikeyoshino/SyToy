using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.ClearCart;

public sealed record ClearCartCommand(Guid OperationId, long ExpectedVersion)
    : AuthorizedCartRequest<Result<CartMutationResult>>
{
    public override Result<CartMutationResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CartMutationResult>.Failure(requestError, validationFailures);
}
