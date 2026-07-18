namespace ToyStore.Application.Orders;

public interface ICustomerOrderReader
{
    Task<CustomerOrderPage> ListAsync(
        string customerId,
        int page,
        int pageSize,
        string? searchTerm,
        CancellationToken cancellationToken);

    Task<CustomerOrderDetailView?> GetAsync(
        string customerId,
        string orderNumber,
        CancellationToken cancellationToken);
}
