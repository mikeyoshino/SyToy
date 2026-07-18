using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.ListCustomerOrders;

public sealed record ListCustomerOrdersQuery(
    int Page = 1,
    int PageSize = 12,
    string? SearchTerm = null)
    : AuthorizedResultRequest<Result<CustomerOrderPage>>
{
    public override string RequiredPolicy => PolicyNames.CanViewCustomerOrders;

    public override Result<CustomerOrderPage> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CustomerOrderPage>.Failure(requestError, validationFailures);
}
