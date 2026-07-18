using ToyStore.Domain.Products;

namespace ToyStore.Application.PreOrders;

public interface IPreOrderCheckoutEligibilityReader
{
    Task<PreOrderCheckoutEligibilityReadModel?> ReadAsync(
        Guid productId,
        string customerId,
        CancellationToken cancellationToken);
}

public sealed record PreOrderCheckoutEligibilityReadModel(
    Guid ProductId,
    string DisplayName,
    string EnglishName,
    string Slug,
    ProductStatus Status,
    SaleType SaleType,
    decimal FullPrice,
    decimal DepositAmount,
    DateTimeOffset CloseAtUtc,
    int EstimatedArrivalMonth,
    int EstimatedArrivalYear,
    int BalancePaymentDays,
    Guid CapacityId,
    int TotalCapacity,
    int RemainingCapacity,
    long CapacityVersion,
    int MaxPerCustomer,
    long CustomerAllocatedQuantity);
