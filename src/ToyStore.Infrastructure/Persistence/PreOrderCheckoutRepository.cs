using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ToyStore.Application.Checkout;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Orders;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class PreOrderCheckoutStore(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
    : IPreOrderCheckoutStore
{
    public async Task<Result<PreparedPreOrderCheckout>> PrepareAsync(
        PreparePreOrderCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var existing = await db.CheckoutAttempts.Include(x => x.Items).SingleOrDefaultAsync(x =>
                x.CustomerId == request.CustomerId && x.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing.ProductId == request.ProductId && existing.Quantity == request.Quantity
                    ? Result<PreparedPreOrderCheckout>.Success(ToPrepared(existing))
                    : Result<PreparedPreOrderCheckout>.Failure(CheckoutErrors.PaymentMismatch);
            }

            var product = await db.Products.AsNoTracking()
                .Include(x => x.Images)
                .SingleOrDefaultAsync(x => x.Id == request.ProductId, cancellationToken);
            if (product is null || product.Status != ProductStatus.Published
                || product.SaleType != SaleType.PreOrder || product.PreOrderOffer is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<PreparedPreOrderCheckout>.Failure(CheckoutErrors.NotAvailable);
            }

            var capacities = await db.PreOrderCapacities.FromSqlInterpolated(
                $"SELECT * FROM \"PreOrderCapacities\" WHERE \"ProductId\" = {request.ProductId} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            if (capacities.Length != 1)
                throw new InvalidOperationException("Published Pre-order Product must own exactly one capacity.");

            var capacity = capacities[0];
            var offer = product.PreOrderOffer;
            if (request.NowUtc >= offer.CloseAtUtc || request.NowUtc >= capacity.CloseAtUtc)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<PreparedPreOrderCheckout>.Failure(PreOrderCapacityErrors.Closed);
            }
            if (request.Quantity > capacity.RemainingQuantity)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<PreparedPreOrderCheckout>.Failure(PreOrderCapacityErrors.InsufficientCapacity);
            }
            var allocated = await db.PreOrderCapacityReservations.Where(x =>
                x.ProductId == request.ProductId && x.CustomerId == request.CustomerId
                && (x.Status == PreOrderCapacityReservationStatus.Active
                    || x.Status == PreOrderCapacityReservationStatus.Consumed))
                .SumAsync(x => x.Quantity, cancellationToken);
            if ((long)allocated + request.Quantity > offer.MaxPerCustomer)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<PreparedPreOrderCheckout>.Failure(PreOrderCapacityErrors.CustomerLimitExceeded);
            }

            var categoryName = await db.ProductCategories.Where(x => x.Id == product.ProductCategoryId)
                .Select(x => x.Code).SingleAsync(cancellationToken);
            var brandName = await db.Brands.Where(x => x.Id == product.BrandId)
                .Select(x => x.DisplayName).SingleAsync(cancellationToken);
            var universeName = await db.Universes.Where(x => x.Id == product.UniverseId)
                .Select(x => x.DisplayName).SingleAsync(cancellationToken);
            var primaryImage = product.Images.SingleOrDefault(x => x.IsPrimary)
                ?? throw new InvalidOperationException("Published Product must have one primary image.");
            var expiresAt = request.NowUtc + PreOrderCapacityPolicy.ReservationLifetime;
            var checkout = CheckoutAttempt.CreatePreOrder(request.CheckoutAttemptId, request.CustomerId,
                product.Id, capacity.Id, request.ReservationId, request.Quantity, product.DisplayName,
                product.EnglishName, product.Slug, categoryName, brandName, universeName,
                primaryImage.PublicRelativeUrl, offer.FullPrice.Amount, offer.DepositAmount.Amount,
                offer.CloseAtUtc, offer.EstimatedArrival.Month, offer.EstimatedArrival.Year,
                offer.BalancePaymentDays, request.Address, request.IdempotencyKey, request.NowUtc, expiresAt);
            var reservation = capacity.Reserve(request.ReservationId, request.CheckoutAttemptId,
                request.CustomerId, request.Quantity, request.NowUtc, expiresAt, request.ReserveMovementId,
                "เริ่มชำระมัดจำพรีออเดอร์", request.IdempotencyKey, capacity.Version, request.CustomerId);
            db.CheckoutAttempts.Add(checkout);
            db.PreOrderCapacityReservations.Add(reservation.Reservation);
            db.PreOrderCapacityMovements.Add(reservation.Movement);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PreparedPreOrderCheckout>.Success(ToPrepared(checkout));
        }
        catch (DbUpdateException exception) when (IsCheckoutConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PreparedPreOrderCheckout>.Failure(PreOrderCapacityErrors.OperationConflict);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PreparedPreOrderCheckout>> AttachProviderSessionAsync(
        string customerId,
        Guid checkoutAttemptId,
        string providerSessionId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var checkout = await db.CheckoutAttempts.Include(x => x.Items).SingleOrDefaultAsync(x =>
            x.Id == checkoutAttemptId && x.CustomerId == customerId, cancellationToken);
        if (checkout is null)
            return Result<PreparedPreOrderCheckout>.Failure(CheckoutErrors.NotFound);
        try
        {
            checkout.AttachProviderSession(providerSessionId, timeProvider.GetUtcNow().ToUniversalTime());
            await db.SaveChangesAsync(cancellationToken);
            return Result<PreparedPreOrderCheckout>.Success(ToPrepared(checkout));
        }
        catch (InvalidOperationException)
        {
            return Result<PreparedPreOrderCheckout>.Failure(CheckoutErrors.PaymentMismatch);
        }
    }

    public async Task<Result<FulfilledPreOrderCheckout>> FulfillAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (!evidence.IsPaid || evidence.CheckoutAttemptId is null
            || string.IsNullOrWhiteSpace(evidence.PaymentReference))
            return Result<FulfilledPreOrderCheckout>.Failure(CheckoutErrors.PaymentMismatch);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var checkoutRows = await db.CheckoutAttempts.FromSqlInterpolated(
                $"SELECT * FROM \"CheckoutAttempts\" WHERE \"Id\" = {evidence.CheckoutAttemptId.Value} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            var checkout = checkoutRows.SingleOrDefault();
            if (checkout is null)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<FulfilledPreOrderCheckout>.Failure(CheckoutErrors.NotFound);
            }
            await db.Entry(checkout).Collection(x => x.Items).LoadAsync(cancellationToken);
            var expectedMinor = decimal.ToInt64(decimal.Round(checkout.PaymentAmount * 100m, 0));
            if (!string.Equals(checkout.ProviderSessionId, evidence.SessionId, StringComparison.Ordinal)
                || evidence.AmountTotalMinor != expectedMinor
                || !string.Equals(evidence.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<FulfilledPreOrderCheckout>.Failure(CheckoutErrors.PaymentMismatch);
            }

            var existingOrder = await db.Orders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CheckoutAttemptId == checkout.Id, cancellationToken);
            if (existingOrder is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<FulfilledPreOrderCheckout>.Success(new(existingOrder.Id, existingOrder.Number, false));
            }

            var capacity = (await db.PreOrderCapacities.FromSqlInterpolated(
                $"SELECT * FROM \"PreOrderCapacities\" WHERE \"Id\" = {checkout.CapacityId} FOR UPDATE")
                .ToArrayAsync(cancellationToken)).Single();
            var reservation = await db.PreOrderCapacityReservations.SingleAsync(x =>
                x.Id == checkout.ReservationId && x.CheckoutAttemptId == checkout.Id, cancellationToken);
            var consumed = capacity.ConsumeReservation(reservation, Guid.NewGuid(),
                "Stripe ยืนยันการชำระมัดจำ", evidence.EventId, capacity.Version,
                evidence.OccurredAtUtc.ToUniversalTime(), "stripe-webhook");
            if (consumed.Movement is not null)
                db.PreOrderCapacityMovements.Add(consumed.Movement);

            var orderId = Guid.NewGuid();
            var orderNumber = $"SY-{checkout.CreatedAtUtc:yyyyMMdd}-{checkout.Id.ToString("N")[..10].ToUpperInvariant()}";
            var order = Order.CreatePreOrder(orderId, orderNumber, checkout, evidence.OccurredAtUtc.ToUniversalTime());
            var payment = Payment.CreateDeposit(Guid.NewGuid(), orderId, checkout.Id, checkout.PaymentAmount,
                checkout.Currency, evidence.SessionId, evidence.PaymentReference!, evidence.EventId,
                evidence.OccurredAtUtc.ToUniversalTime());
            db.Orders.Add(order);
            db.Payments.Add(payment);
            checkout.Complete(evidence.OccurredAtUtc.ToUniversalTime());
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<FulfilledPreOrderCheckout>.Success(new(order.Id, order.Number, true));
        }
        catch (DbUpdateException exception) when (IsCheckoutConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await using var replay = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await replay.Orders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CheckoutAttemptId == evidence.CheckoutAttemptId, cancellationToken);
            return existing is null
                ? Result<FulfilledPreOrderCheckout>.Failure(CheckoutErrors.PaymentMismatch)
                : Result<FulfilledPreOrderCheckout>.Success(new(existing.Id, existing.Number, false));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PreOrderCheckoutStatusResult>> GetStatusAsync(
        string customerId,
        Guid checkoutAttemptId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var checkout = await db.CheckoutAttempts.AsNoTracking().Include(x => x.Items).SingleOrDefaultAsync(x =>
            x.Id == checkoutAttemptId && x.CustomerId == customerId, cancellationToken);
        if (checkout is null)
            return Result<PreOrderCheckoutStatusResult>.Failure(CheckoutErrors.NotFound);
        var orderNumber = await db.Orders.AsNoTracking().Where(x => x.CheckoutAttemptId == checkout.Id)
            .Select(x => x.Number).SingleOrDefaultAsync(cancellationToken);
        return Result<PreOrderCheckoutStatusResult>.Success(new(checkout.Id,
            checkout.Status.ToString(), checkout.DisplayName, checkout.PaymentAmount, orderNumber));
    }

    public async Task<Result<ExpiredCheckoutResult>> ExpireAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (evidence.CheckoutAttemptId is null)
            return Result<ExpiredCheckoutResult>.Failure(CheckoutErrors.PaymentMismatch);
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var rows = await db.CheckoutAttempts.FromSqlInterpolated(
                $"SELECT * FROM \"CheckoutAttempts\" WHERE \"Id\" = {evidence.CheckoutAttemptId.Value} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            var checkout = rows.SingleOrDefault();
            if (checkout is null || checkout.SaleType != SaleType.PreOrder)
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<ExpiredCheckoutResult>.Failure(CheckoutErrors.NotFound);
            }
            if (!string.Equals(checkout.ProviderSessionId, evidence.SessionId, StringComparison.Ordinal))
            {
                await transaction.RollbackAsync(CancellationToken.None);
                return Result<ExpiredCheckoutResult>.Failure(CheckoutErrors.PaymentMismatch);
            }
            if (checkout.Status is CheckoutAttemptStatus.Completed or CheckoutAttemptStatus.Expired)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<ExpiredCheckoutResult>.Success(new(checkout.Id, false));
            }
            await db.Entry(checkout).Collection(x => x.Items).LoadAsync(cancellationToken);
            var expiredAt = evidence.OccurredAtUtc.ToUniversalTime() < checkout.ExpiresAtUtc
                ? checkout.ExpiresAtUtc
                : evidence.OccurredAtUtc.ToUniversalTime();
            var capacity = (await db.PreOrderCapacities.FromSqlInterpolated(
                $"SELECT * FROM \"PreOrderCapacities\" WHERE \"Id\" = {checkout.CapacityId} FOR UPDATE")
                .ToArrayAsync(cancellationToken)).Single();
            var reservation = await db.PreOrderCapacityReservations.SingleAsync(x =>
                x.Id == checkout.ReservationId && x.CheckoutAttemptId == checkout.Id,
                cancellationToken);
            var transition = capacity.ExpireReservation(reservation, Guid.NewGuid(),
                "Stripe checkout หมดอายุ", evidence.EventId, capacity.Version,
                expiredAt, "stripe-webhook");
            if (transition.Movement is not null) db.PreOrderCapacityMovements.Add(transition.Movement);
            checkout.Expire(expiredAt);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<ExpiredCheckoutResult>.Success(new(checkout.Id, true));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    private static PreparedPreOrderCheckout ToPrepared(CheckoutAttempt checkout) => new(
        checkout.Id, checkout.CustomerId, checkout.IdempotencyKey, checkout.DisplayName,
        checkout.Quantity, checkout.FullPrice, checkout.DepositAmount, checkout.BalanceAmount,
        checkout.PaymentAmount, checkout.Currency, checkout.ExpiresAtUtc, checkout.ProviderSessionId);

    private static bool IsCheckoutConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState is PostgresErrorCodes.UniqueViolation or PostgresErrorCodes.SerializationFailure;
}
