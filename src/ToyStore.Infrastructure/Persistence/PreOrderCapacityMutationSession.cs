using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class PreOrderCapacityMutationSessionFactory(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IPreOrderCapacityMutationSessionFactory
{
    public async ValueTask<IPreOrderCapacityMutationSession> OpenAsync(
        CancellationToken cancellationToken) =>
        new PreOrderCapacityMutationSession(
            await contextFactory.CreateDbContextAsync(cancellationToken));
}

internal sealed class PreOrderCapacityMutationSession(ApplicationDbContext dbContext)
    : IPreOrderCapacityMutationSession
{
    private IDbContextTransaction? transaction;
    private bool active;
    private bool locked;
    private bool disposed;
    private Guid capacityId;
    private Guid productId;

    public async Task<Result<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        if (active || transaction is not null)
        {
            throw new InvalidOperationException("Pre-order capacity session executes only once.");
        }

        transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        active = true;
        try
        {
            var result = await operation(cancellationToken);
            if (result.IsFailure)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return result;
            }

            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return Result<T>.Failure(PreOrderCapacityErrors.StaleVersion);
            }
            catch (DbUpdateException exception) when (MapPersistenceConflict(exception) is { } error)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                dbContext.ChangeTracker.Clear();
                return Result<T>.Failure(error);
            }

            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception)
        {
            try
            {
                await transaction.RollbackAsync(CancellationToken.None);
            }
            catch (Exception rollbackException)
            {
                throw new AggregateException(
                    "Pre-order mutation and rollback both failed.", exception, rollbackException);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
        finally
        {
            active = false;
            await DisposeTransactionAsync();
        }
    }

    public async Task<LockedPreOrderCapacity?> LockCapacityAsync(
        Guid requestedCapacityId,
        Guid requestedProductId,
        CancellationToken cancellationToken)
    {
        EnsureActive();
        if (locked)
        {
            throw new InvalidOperationException("Pre-order session can lock only one capacity.");
        }

        locked = true;
        capacityId = requestedCapacityId;
        productId = requestedProductId;
        var matches = await dbContext.PreOrderCapacities
            .FromSqlInterpolated(
            $"""
            SELECT * FROM "PreOrderCapacities"
            WHERE "Id" = {requestedCapacityId} AND "ProductId" = {requestedProductId}
            FOR UPDATE
            """)
            .ToListAsync(cancellationToken);
        var capacity = matches.SingleOrDefault();
        if (capacity is null)
        {
            return null;
        }

        var maxPerCustomer = await dbContext.Products
            .Where(product => product.Id == requestedProductId)
            .Select(product => product.PreOrderOffer == null
                ? 0
                : product.PreOrderOffer.MaxPerCustomer)
            .SingleAsync(cancellationToken);
        if (maxPerCustomer <= 0)
        {
            throw new InvalidOperationException("Persisted pre-order capacity has no valid Product offer.");
        }

        return new LockedPreOrderCapacity(capacity, maxPerCustomer);
    }

    public async Task<PreOrderCapacityReservation?> FindReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken)
    {
        EnsureLocked();
        var reservation = await dbContext.PreOrderCapacityReservations.SingleOrDefaultAsync(
            item => item.Id == reservationId
                && item.CapacityId == capacityId
                && item.ProductId == productId,
            cancellationToken);
        return reservation;
    }

    public Task<PreOrderCapacityMovement?> FindMovementAsync(
        Guid movementId,
        CancellationToken cancellationToken)
    {
        EnsureLocked();
        return dbContext.PreOrderCapacityMovements.AsNoTracking().SingleOrDefaultAsync(
            item => item.Id == movementId,
            cancellationToken);
    }

    public Task<int> CustomerAllocatedQuantityAsync(
        Guid requestedProductId,
        string customerId,
        CancellationToken cancellationToken)
    {
        EnsureLocked();
        if (requestedProductId != productId)
        {
            throw new InvalidOperationException("Customer allocation query does not match locked Product.");
        }

        return dbContext.PreOrderCapacityReservations
            .Where(item => item.ProductId == requestedProductId
                && item.CustomerId == customerId
                && (item.Status == PreOrderCapacityReservationStatus.Active
                    || item.Status == PreOrderCapacityReservationStatus.Consumed))
            .SumAsync(item => item.Quantity, cancellationToken);
    }

    public void Add(PreOrderCapacityReservation reservation)
    {
        EnsureLocked();
        EnsureOwnership(reservation.CapacityId, reservation.ProductId);
        dbContext.PreOrderCapacityReservations.Add(reservation);
    }

    public void Add(PreOrderCapacityMovement movement)
    {
        EnsureLocked();
        EnsureOwnership(movement.CapacityId, movement.ProductId);
        dbContext.PreOrderCapacityMovements.Add(movement);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        await DisposeTransactionAsync();
        await dbContext.DisposeAsync();
    }

    private void EnsureActive()
    {
        if (!active)
        {
            throw new InvalidOperationException("Pre-order session operation is not active.");
        }
    }

    private void EnsureLocked()
    {
        EnsureActive();
        if (!locked)
        {
            throw new InvalidOperationException("Pre-order capacity must be locked first.");
        }
    }

    private void EnsureOwnership(Guid ownerCapacityId, Guid ownerProductId)
    {
        if (ownerCapacityId != capacityId || ownerProductId != productId)
        {
            throw new InvalidOperationException("Pre-order evidence does not match locked capacity.");
        }
    }

    private async ValueTask DisposeTransactionAsync()
    {
        if (transaction is null)
        {
            return;
        }

        await transaction.DisposeAsync();
        transaction = null;
    }

    private static Error? MapPersistenceConflict(DbUpdateException exception)
    {
        if (exception.InnerException is not PostgresException postgres)
        {
            return null;
        }

        return postgres.ConstraintName switch
        {
            "UX_PreOrderCapacityMovements_CapacityId_Version" =>
                PreOrderCapacityErrors.StaleVersion,
            "PK_PreOrderCapacityMovements"
                or "PK_PreOrderCapacityReservations"
                or "UX_PreOrderCapacityReservations_CheckoutAttemptId"
                or "UX_PreOrderCapacityReservations_ReserveMovementId"
                or "UX_PreOrderCapacityReservations_TransitionMovementId" =>
                PreOrderCapacityErrors.OperationConflict,
            _ => null,
        };
    }
}
