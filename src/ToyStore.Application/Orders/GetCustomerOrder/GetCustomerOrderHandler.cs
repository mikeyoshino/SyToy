using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.GetCustomerOrder;

public sealed class GetCustomerOrderHandler(ICustomerOrderReader reader)
    : IRequestHandler<GetCustomerOrderQuery, Result<CustomerOrderDetailView>>
{
    public async Task<Result<CustomerOrderDetailView>> Handle(
        GetCustomerOrderQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "Customer Order query reached its handler without an authorized customer.");
        var order = await reader.GetAsync(
            customerId,
            request.OrderNumber.Trim(),
            cancellationToken);
        return order is null
            ? Result<CustomerOrderDetailView>.Failure(CustomerOrderErrors.NotFound)
            : Result<CustomerOrderDetailView>.Success(order);
    }
}
