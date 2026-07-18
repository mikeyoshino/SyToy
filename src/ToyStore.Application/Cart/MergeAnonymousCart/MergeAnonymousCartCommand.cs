using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Cart.MergeAnonymousCart;

public sealed record AnonymousCartInput(Guid ProductId, int Quantity);

public sealed record MergeAnonymousCartCommand(
    Guid OperationId,
    IReadOnlyList<AnonymousCartInput> Items)
    : AuthorizedCartRequest<Result<MergeAnonymousCartResult>>
{
    public override Result<MergeAnonymousCartResult> CreateFailure(Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<MergeAnonymousCartResult>.Failure(requestError, validationFailures);
}

public sealed record MergeAnonymousCartResult(
    CartMutationResult Cart,
    IReadOnlyList<MergeRejectedItem> RejectedItems,
    IReadOnlyList<MergeClampedItem> ClampedItems);

public sealed record MergeRejectedItem(Guid ProductId, string Message);

public sealed record MergeClampedItem(Guid ProductId, int RequestedQuantity, int AppliedQuantity);
