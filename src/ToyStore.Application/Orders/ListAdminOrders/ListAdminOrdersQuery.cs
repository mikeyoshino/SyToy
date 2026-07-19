using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.ListAdminOrders;

public sealed record ListAdminOrdersQuery(
    string? Search = null,
    AdminOrderSaleType? SaleType = null,
    AdminOrderPaymentStatus? PaymentStatus = null,
    AdminOrderFulfillmentStatus? FulfillmentStatus = null,
    DateOnly? CreatedFrom = null,
    DateOnly? CreatedTo = null,
    int Page = 1,
    int PageSize = 20)
    : AuthorizedResultRequest<Result<AdminOrderPage>>
{
    public override string RequiredPolicy => PolicyNames.CanManageOrders;

    public override Result<AdminOrderPage> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<AdminOrderPage>.Failure(requestError, validationFailures);
}
