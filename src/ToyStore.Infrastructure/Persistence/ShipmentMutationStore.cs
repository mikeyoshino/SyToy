using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Orders;
using ToyStore.Domain.Orders;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class ShipmentMutationStore(IDbContextFactory<ApplicationDbContext> contextFactory)
    : IShipmentMutationStore
{
    public async Task<Result<CreateShipmentResult>> CreateAsync(ShipmentMutationRequest request, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var existing = await db.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
            if (existing is not null)
            {
                var existingOrder = await db.Orders.AsNoTracking().SingleAsync(x => x.Id == existing.OrderId, cancellationToken);
                await tx.CommitAsync(cancellationToken);
                return Result<CreateShipmentResult>.Success(ToResult(existingOrder, existing, false));
            }

            var rows = await db.Orders.FromSqlInterpolated($"SELECT * FROM \"Orders\" WHERE \"Number\" = {request.OrderNumber} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            var order = rows.SingleOrDefault();
            if (order is null) return await Failure(AdminOrderErrors.NotFound, tx);
            if (await db.Shipments.AnyAsync(x => x.OrderId == order.Id, cancellationToken))
                return await Failure(AdminOrderErrors.ShipmentConflict, tx);
            if (order.Version != request.ExpectedVersion)
                return await Failure(AdminOrderErrors.Stale, tx);
            if (order.PaymentStatus != PaymentStatus.Paid || order.FulfillmentStatus != FulfillmentStatus.ReadyToShip)
                return await Failure(AdminOrderErrors.InvalidShipmentState, tx);

            var shipment = Shipment.Create(Guid.NewGuid(), order.Id, request.OperationId, request.Carrier,
                request.TrackingNumber, request.OtherTrackingUrl, request.NowUtc, request.ActorId);
            order.MarkShipped(request.ExpectedVersion, request.NowUtc);
            db.Shipments.Add(shipment);
            db.OrderAuditEvents.Add(OrderAuditEvent.ShipmentCreated(Guid.NewGuid(), order.Id,
                request.OperationId, request.ActorId, request.Carrier, shipment.TrackingNumber, request.NowUtc));
            await db.SaveChangesAsync(cancellationToken);
            await tx.CommitAsync(cancellationToken);
            return Result<CreateShipmentResult>.Success(ToResult(order, shipment, true));
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation)
        {
            await tx.RollbackAsync(CancellationToken.None);
            await using var replay = await contextFactory.CreateDbContextAsync(cancellationToken);
            var shipment = await replay.Shipments.AsNoTracking().SingleOrDefaultAsync(x => x.OperationId == request.OperationId, cancellationToken);
            if (shipment is null) return Result<CreateShipmentResult>.Failure(AdminOrderErrors.ShipmentConflict);
            var order = await replay.Orders.AsNoTracking().SingleAsync(x => x.Id == shipment.OrderId, cancellationToken);
            return Result<CreateShipmentResult>.Success(ToResult(order, shipment, false));
        }
    }

    private static CreateShipmentResult ToResult(Order order, Shipment shipment, bool changed) =>
        new(order.Number, shipment.Carrier.ToString(), shipment.TrackingNumber, shipment.TrackingUrl,
            shipment.ShippedAtUtc, order.Version, changed);

    private static async Task<Result<CreateShipmentResult>> Failure(Error error,
        Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction tx)
    {
        await tx.RollbackAsync(CancellationToken.None);
        return Result<CreateShipmentResult>.Failure(error);
    }
}
