using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Orders;

public sealed record CustomerOrderItemView(
    Guid ProductId,
    string DisplayName,
    string EnglishName,
    string ProductSlug,
    string CategoryName,
    string BrandName,
    string UniverseName,
    string PrimaryImageUrl,
    SaleType SaleType,
    int Quantity,
    decimal FullPrice,
    decimal DepositAmount,
    decimal BalanceAmount,
    decimal LinePaidAmount,
    DateTimeOffset? PreOrderCloseAtUtc,
    int? EstimatedArrivalMonth,
    int? EstimatedArrivalYear,
    int? BalancePaymentDays,
    string? DepositPolicy);

public sealed record CustomerOrderSummaryView(
    string Number,
    SaleType SaleType,
    PaymentStatus PaymentStatus,
    FulfillmentStatus FulfillmentStatus,
    decimal ShippingAmount,
    decimal TotalPaid,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<CustomerOrderItemView> Items);

public sealed record CustomerOrderPage(
    IReadOnlyList<CustomerOrderSummaryView> Items,
    int Page,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalCount / (double)PageSize));
}

public sealed record CustomerOrderAddressView(
    string RecipientName,
    string PhoneNumber,
    string AddressLine,
    string SubDistrict,
    string District,
    string Province,
    string PostalCode);

public sealed record CustomerOrderDetailView(
    string Number,
    SaleType SaleType,
    PaymentStatus PaymentStatus,
    FulfillmentStatus FulfillmentStatus,
    CustomerOrderAddressView Address,
    decimal ShippingAmount,
    decimal TotalPaid,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<CustomerOrderItemView> Items);
