using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.RemoveCartItem;

public sealed record RemoveCartItemCommand(
    Guid OperationId,
    Guid ProductId,
    long ExpectedVersion) : AuthorizedCartRequest<Result<CartMutationResult>>
{
    public override Result<CartMutationResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CartMutationResult>.Failure(requestError, validationFailures);
}
