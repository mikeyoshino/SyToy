using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Inventory.ReceiveStock;

public sealed record ReceiveStockCommand(
    Guid InventoryItemId,
    Guid ProductId,
    Guid OperationId,
    long ExpectedVersion,
    int Quantity,
    string Reason,
    string Reference)
    : AuthorizedInventoryMutationRequest<Result<InventoryMutationResult>>
{
    public override Result<InventoryMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<InventoryMutationResult>.Failure(requestError, validationFailures);
}
