using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Products;

namespace ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;

public sealed class GetPreOrderCheckoutEligibilityHandler(
    IPreOrderCheckoutEligibilityReader reader,
    TimeProvider timeProvider)
    : IRequestHandler<GetPreOrderCheckoutEligibilityQuery, Result<PreOrderCheckoutEligibilityResult>>
{
    public async Task<Result<PreOrderCheckoutEligibilityResult>> Handle(
        GetPreOrderCheckoutEligibilityQuery request,
        CancellationToken cancellationToken)
    {
        var customerId = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "Pre-order checkout eligibility requires an authorized customer.");
        var model = await reader.ReadAsync(request.ProductId, customerId, cancellationToken);
        if (model is null
            || model.Status != ProductStatus.Published
            || model.SaleType != SaleType.PreOrder)
        {
            return Result<PreOrderCheckoutEligibilityResult>.Failure(
                PreOrderCapacityErrors.NotAvailable);
        }

        EnsureCoherentSnapshot(model, request.ProductId);
        var now = timeProvider.GetUtcNow().ToUniversalTime();
        if (now >= model.CloseAtUtc)
        {
            return Result<PreOrderCheckoutEligibilityResult>.Failure(PreOrderCapacityErrors.Closed);
        }

        if (request.Quantity > model.RemainingCapacity)
        {
            return Result<PreOrderCheckoutEligibilityResult>.Failure(
                PreOrderCapacityErrors.InsufficientCapacity);
        }

        var requestedAllocation = model.CustomerAllocatedQuantity + (long)request.Quantity;
        if (requestedAllocation > model.MaxPerCustomer)
        {
            return Result<PreOrderCheckoutEligibilityResult>.Failure(
                PreOrderCapacityErrors.CustomerLimitExceeded);
        }

        var allocated = checked((int)model.CustomerAllocatedQuantity);
        return Result<PreOrderCheckoutEligibilityResult>.Success(new(
            model.ProductId,
            model.DisplayName,
            model.EnglishName,
            model.Slug,
            model.FullPrice,
            model.DepositAmount,
            model.FullPrice - model.DepositAmount,
            model.CloseAtUtc,
            model.EstimatedArrivalMonth,
            model.EstimatedArrivalYear,
            model.CapacityId,
            model.TotalCapacity,
            model.RemainingCapacity,
            model.CapacityVersion,
            model.MaxPerCustomer,
            allocated,
            model.MaxPerCustomer - allocated,
            request.Quantity,
            model.BalancePaymentDays,
            PreOrderDepositPolicy.NonRefundableOnCustomerCancellationOrBalanceOverdue,
            now));
    }

    private static void EnsureCoherentSnapshot(
        PreOrderCheckoutEligibilityReadModel model,
        Guid requestedProductId)
    {
        if (model.ProductId != requestedProductId
            || model.CapacityId == Guid.Empty
            || model.FullPrice <= 0
            || model.DepositAmount <= 0
            || model.DepositAmount >= model.FullPrice
            || model.CloseAtUtc.Offset != TimeSpan.Zero
            || model.EstimatedArrivalMonth is < 1 or > 12
            || model.EstimatedArrivalYear is < 1 or > 9999
            || model.BalancePaymentDays <= 0
            || model.TotalCapacity <= 0
            || model.RemainingCapacity < 0
            || model.RemainingCapacity > model.TotalCapacity
            || model.CapacityVersion <= 0
            || model.MaxPerCustomer <= 0
            || model.MaxPerCustomer > model.TotalCapacity
            || model.CustomerAllocatedQuantity < 0
            || model.CustomerAllocatedQuantity > model.TotalCapacity)
        {
            throw new InvalidOperationException(
                "Persisted Pre-order checkout eligibility snapshot is incoherent.");
        }
    }
}
