namespace ToyStore.Application.Orders;

public interface IAdminOrderReader
{
    Task<AdminOrderPage> ListAsync(
        AdminOrderReadRequest request,
        CancellationToken cancellationToken);

    Task<AdminOrderDetailView?> GetAsync(
        string orderNumber,
        CancellationToken cancellationToken);
}

public sealed record AdminOrderReadRequest(
    string? Search,
    AdminOrderSaleType? SaleType,
    AdminOrderPaymentStatus? PaymentStatus,
    AdminOrderFulfillmentStatus? FulfillmentStatus,
    DateTimeOffset? CreatedFromUtc,
    DateTimeOffset? CreatedBeforeUtc,
    int Page,
    int PageSize);
