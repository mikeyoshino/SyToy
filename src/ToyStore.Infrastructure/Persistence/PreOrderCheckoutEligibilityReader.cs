using System.Data;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.PreOrders;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class PreOrderCheckoutEligibilityReader(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IPreOrderCheckoutEligibilityReader
{
    public async Task<PreOrderCheckoutEligibilityReadModel?> ReadAsync(
        Guid productId,
        string customerId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.RepeatableRead,
            cancellationToken);
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == productId,
            cancellationToken);
        if (product is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (product.Status != ProductStatus.Published
            || product.SaleType != SaleType.PreOrder)
        {
            await transaction.CommitAsync(cancellationToken);
            return Unavailable(product);
        }

        var offer = product.PreOrderOffer
            ?? throw Incoherent("Published Pre-order Product has no offer.");
        var capacities = await db.PreOrderCapacities.AsNoTracking()
            .Where(item => item.ProductId == product.Id)
            .Take(2)
            .ToArrayAsync(cancellationToken);
        if (capacities.Length != 1)
        {
            throw Incoherent("Published Pre-order Product must own exactly one capacity.");
        }

        var capacity = capacities[0];
        var reservations = await db.PreOrderCapacityReservations.AsNoTracking()
            .Where(item => item.CapacityId == capacity.Id)
            .Select(item => new ReservationSnapshot(
                item.ProductId,
                item.CustomerId,
                item.Quantity,
                item.Status,
                item.TransitionAtUtc))
            .ToArrayAsync(cancellationToken);
        var movements = await db.PreOrderCapacityMovements.AsNoTracking()
            .Where(item => item.CapacityId == capacity.Id)
            .OrderBy(item => item.ResultingCapacityVersion)
            .ToArrayAsync(cancellationToken);

        EnsureCoherent(product, offer, capacity, reservations, movements);
        var allocated = reservations
            .Where(item => item.CustomerId == customerId
                && item.Status is PreOrderCapacityReservationStatus.Active
                    or PreOrderCapacityReservationStatus.Consumed)
            .Sum(item => (long)item.Quantity);
        var model = new PreOrderCheckoutEligibilityReadModel(
            product.Id,
            product.DisplayName,
            product.EnglishName,
            product.Slug,
            product.Status,
            product.SaleType,
            offer.FullPrice.Amount,
            offer.DepositAmount.Amount,
            offer.CloseAtUtc,
            offer.EstimatedArrival.Month,
            offer.EstimatedArrival.Year,
            offer.BalancePaymentDays,
            capacity.Id,
            capacity.TotalCapacity,
            capacity.RemainingQuantity,
            capacity.Version,
            offer.MaxPerCustomer,
            allocated);
        await transaction.CommitAsync(cancellationToken);
        return model;
    }

    private static PreOrderCheckoutEligibilityReadModel Unavailable(Product product) => new(
        product.Id,
        product.DisplayName,
        product.EnglishName,
        product.Slug,
        product.Status,
        product.SaleType,
        0,
        0,
        DateTimeOffset.UnixEpoch,
        0,
        0,
        0,
        Guid.Empty,
        0,
        0,
        0,
        0,
        0);

    private static void EnsureCoherent(
        Product product,
        PreOrderOffer offer,
        PreOrderCapacity capacity,
        IReadOnlyCollection<ReservationSnapshot> reservations,
        PreOrderCapacityMovement[] movements)
    {
        if (capacity.ProductId != product.Id
            || capacity.CloseAtUtc.Offset != TimeSpan.Zero
            || offer.CloseAtUtc.Offset != TimeSpan.Zero
            || capacity.CloseAtUtc != offer.CloseAtUtc
            || capacity.TotalCapacity != offer.TotalCapacity
            || reservations.Any(item => item.ProductId != product.Id))
        {
            throw Incoherent("Product, offer, capacity or reservation ownership does not match.");
        }

        var active = reservations.Where(item => item.Status == PreOrderCapacityReservationStatus.Active)
            .Sum(item => (long)item.Quantity);
        var consumed = reservations.Where(item => item.Status == PreOrderCapacityReservationStatus.Consumed)
            .Sum(item => (long)item.Quantity);
        var retired = reservations.Where(item =>
                item.Status == PreOrderCapacityReservationStatus.Cancelled
                && item.TransitionAtUtc >= capacity.CloseAtUtc)
            .Sum(item => (long)item.Quantity);
        var accounted = (long)capacity.RemainingQuantity
            + capacity.HeldQuantity
            + capacity.CommittedQuantity
            + capacity.RetiredQuantity;
        if (active != capacity.HeldQuantity
            || consumed != capacity.CommittedQuantity
            || retired != capacity.RetiredQuantity
            || accounted != capacity.TotalCapacity
            || movements.Length != capacity.Version
            || movements.Any(item => item.ProductId != product.Id))
        {
            throw Incoherent("Capacity counters do not match reservation or movement history.");
        }

        var initial = movements.Where(item => item.Type == PreOrderCapacityMovementType.InitialCapacity)
            .ToArray();
        var latest = movements.Length == 0 ? null : movements[^1];
        if (initial.Length != 1
            || initial[0].Quantity != capacity.TotalCapacity
            || initial[0].ResultingCapacityVersion != 1
            || latest is null
            || latest.ResultingCapacityVersion != capacity.Version
            || latest.ResultingRemainingQuantity != capacity.RemainingQuantity
            || latest.ResultingHeldQuantity != capacity.HeldQuantity
            || latest.ResultingCommittedQuantity != capacity.CommittedQuantity
            || latest.ResultingRetiredQuantity != capacity.RetiredQuantity)
        {
            throw Incoherent("Capacity movement evidence does not match current counters.");
        }
    }

    private static InvalidOperationException Incoherent(string detail) => new(
        $"Persisted Pre-order checkout eligibility data is incoherent. {detail}");

    private sealed record ReservationSnapshot(
        Guid ProductId,
        string CustomerId,
        int Quantity,
        PreOrderCapacityReservationStatus Status,
        DateTimeOffset? TransitionAtUtc);
}
