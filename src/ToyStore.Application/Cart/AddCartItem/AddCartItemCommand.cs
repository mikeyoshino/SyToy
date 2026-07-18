using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.AddCartItem;

public sealed record AddCartItemCommand(
    Guid OperationId,
    Guid ProductId,
    int Quantity,
    long ExpectedVersion) : AuthorizedCartRequest<Result<CartMutationResult>>
{
    public override Result<CartMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CartMutationResult>.Failure(requestError, validationFailures);
}
