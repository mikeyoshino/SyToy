namespace ToyStore.Application.Orders;

public enum AdminOrderSaleType { InStock, PreOrder }
public enum AdminOrderPaymentStatus { DepositPaid, Paid, PartiallyRefunded, Refunded }
public enum AdminOrderFulfillmentStatus { AwaitingPreOrderArrival, AwaitingBalancePayment, ReadyToShip, Shipped, Cancelled }
public enum AdminOrderPaymentPurpose { Deposit, Full, Balance, Refund }

public sealed record AdminOrderListItem(
    string Number,
    string CustomerEmail,
    string RecipientName,
    AdminOrderSaleType SaleType,
    AdminOrderPaymentStatus PaymentStatus,
    AdminOrderFulfillmentStatus FulfillmentStatus,
    int ItemCount,
    decimal TotalPaid,
    DateTimeOffset CreatedAtUtc);

public sealed record AdminOrderPage(
    IReadOnlyList<AdminOrderListItem> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record AdminOrderPaymentView(
    AdminOrderPaymentPurpose Purpose,
    decimal Amount,
    string Currency,
    string ProviderPaymentReference,
    DateTimeOffset PaidAtUtc);

public sealed record AdminOrderDetailView(
    string Number,
    string CustomerEmail,
    AdminOrderSaleType SaleType,
    AdminOrderPaymentStatus PaymentStatus,
    AdminOrderFulfillmentStatus FulfillmentStatus,
    CustomerOrderAddressView Address,
    decimal ShippingAmount,
    decimal TotalPaid,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<CustomerOrderItemView> Items,
    IReadOnlyList<AdminOrderPaymentView> Payments,
    long Version,
    CustomerShipmentView? Shipment,
    IReadOnlyList<AdminOrderAuditView> AuditEvents);

public sealed record AdminOrderAuditView(string Action, string ActorId, string Detail, DateTimeOffset OccurredAtUtc);
