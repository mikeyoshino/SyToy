using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.PreOrders.ReservePreOrderCapacity;

public sealed record ReservePreOrderCapacityCommand(
    Guid CapacityId,
    Guid ProductId,
    Guid ReservationId,
    Guid CheckoutAttemptId,
    Guid OperationId,
    long ExpectedVersion,
    int Quantity,
    string Reason,
    string Reference)
    : AuthorizedResultRequest<Result<PreOrderCapacityMutationResult>>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;

    public override Result<PreOrderCapacityMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PreOrderCapacityMutationResult>.Failure(requestError, validationFailures);
}
