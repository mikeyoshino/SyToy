using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.GetAdminOrder;

public sealed record GetAdminOrderQuery(string OrderNumber)
    : AuthorizedResultRequest<Result<AdminOrderDetailView>>
{
    public override string RequiredPolicy => PolicyNames.CanManageOrders;

    public override Result<AdminOrderDetailView> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<AdminOrderDetailView>.Failure(requestError, validationFailures);
}
