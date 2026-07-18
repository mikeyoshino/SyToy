using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.GetCustomerOrder;

public sealed record GetCustomerOrderQuery(string OrderNumber)
    : AuthorizedResultRequest<Result<CustomerOrderDetailView>>
{
    public override string RequiredPolicy => PolicyNames.CanViewCustomerOrders;

    public override Result<CustomerOrderDetailView> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CustomerOrderDetailView>.Failure(requestError, validationFailures);
}
