using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.PreOrders.TransitionPreOrderCapacity;

public sealed record TransitionPreOrderCapacityCommand(
    Guid CapacityId,
    Guid ProductId,
    Guid ReservationId,
    Guid OperationId,
    long ExpectedVersion,
    PreOrderCapacityAction Action,
    string Reason,
    string Reference)
    : AuthorizedResultRequest<Result<PreOrderCapacityMutationResult>>
{
    public override string RequiredPolicy => Action switch
    {
        PreOrderCapacityAction.CancelCustomer => PolicyNames.CanUseCustomerCart,
        PreOrderCapacityAction.CancelAdminOrSupplier or PreOrderCapacityAction.CancelBalanceOverdue => PolicyNames.CanManageOrders,
        _ => PolicyNames.CanVerifyPayments,
    };

    public override Result<PreOrderCapacityMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PreOrderCapacityMutationResult>.Failure(requestError, validationFailures);
}
