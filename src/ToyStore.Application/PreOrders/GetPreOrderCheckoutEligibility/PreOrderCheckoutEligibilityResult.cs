namespace ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;

public enum PreOrderDepositPolicy
{
    NonRefundableOnCustomerCancellationOrBalanceOverdue,
}

public sealed record PreOrderCheckoutEligibilityResult(
    Guid ProductId,
    string DisplayName,
    string EnglishName,
    string Slug,
    decimal FullPrice,
    decimal DepositAmount,
    decimal BalanceAmount,
    DateTimeOffset CloseAtUtc,
    int EstimatedArrivalMonth,
    int EstimatedArrivalYear,
    Guid CapacityId,
    int TotalCapacity,
    int RemainingCapacity,
    long CapacityVersion,
    int MaxPerCustomer,
    int CustomerAllocatedQuantity,
    int CustomerRemainingAllowance,
    int RequestedQuantity,
    int BalancePaymentDays,
    PreOrderDepositPolicy DepositPolicy,
    DateTimeOffset CheckedAtUtc);
