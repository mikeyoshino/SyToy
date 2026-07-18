using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.ListCustomerOrders;

public sealed class ListCustomerOrdersHandler(ICustomerOrderReader reader)
    : IRequestHandler<ListCustomerOrdersQuery, Result<CustomerOrderPage>>
{
    public async Task<Result<CustomerOrderPage>> Handle(
        ListCustomerOrdersQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "Customer Order query reached its handler without an authorized customer.");
        var page = await reader.ListAsync(
            customerId,
            request.Page,
            request.PageSize,
            request.SearchTerm,
            cancellationToken);
        return Result<CustomerOrderPage>.Success(page);
    }
}
