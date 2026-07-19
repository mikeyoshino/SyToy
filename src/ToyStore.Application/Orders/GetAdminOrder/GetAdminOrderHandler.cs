using MediatR;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Orders.GetAdminOrder;

public sealed class GetAdminOrderHandler(IAdminOrderReader reader)
    : IRequestHandler<GetAdminOrderQuery, Result<AdminOrderDetailView>>
{
    public async Task<Result<AdminOrderDetailView>> Handle(
        GetAdminOrderQuery request,
        CancellationToken cancellationToken)
    {
        var order = await reader.GetAsync(request.OrderNumber.Trim(), cancellationToken);
        return order is null
            ? Result<AdminOrderDetailView>.Failure(AdminOrderErrors.NotFound)
            : Result<AdminOrderDetailView>.Success(order);
    }
}
