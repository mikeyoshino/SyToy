using System.Data;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ToyStore.Application.Checkout;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class InStockCheckoutStore(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TimeProvider timeProvider)
    : IInStockCheckoutStore
{
    public async Task<Result<PreparedInStockCheckout>> PrepareAsync(
        PrepareInStockCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        // Product inventory rows are locked in deterministic order below. ReadCommitted lets
        // the losing checkout observe the committed hold and return a business stock failure
        // instead of surfacing PostgreSQL's serializable-retry exception.
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        try
        {
            var existing = await db.CheckoutAttempts.Include(x => x.Items).SingleOrDefaultAsync(x =>
                x.CustomerId == request.CustomerId && x.IdempotencyKey == request.IdempotencyKey,
                cancellationToken);
            if (existing is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return existing.SaleType == SaleType.InStock
                    ? Result<PreparedInStockCheckout>.Success(ToPrepared(existing))
                    : Result<PreparedInStockCheckout>.Failure(CheckoutErrors.PaymentMismatch);
            }

            var users = await db.Users.FromSqlInterpolated(
                $"SELECT * FROM \"AspNetUsers\" WHERE \"Id\" = {request.CustomerId} FOR UPDATE")
                .AsNoTracking().ToArrayAsync(cancellationToken);
            if (users.Length != 1)
                return await RollbackFailure(CheckoutErrors.NotFound, transaction);

            var carts = await db.Carts.FromSqlInterpolated(
                $"SELECT * FROM \"Carts\" WHERE \"CustomerId\" = {request.CustomerId} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            if (carts.Length != 1)
                return await RollbackFailure(CheckoutErrors.CartEmpty, transaction);
            var cart = carts[0];
            await db.Entry(cart).Collection(x => x.Items).LoadAsync(cancellationToken);
            if (cart.Items.Count == 0)
                return await RollbackFailure(CheckoutErrors.CartEmpty, transaction);

            var cartLines = cart.Items.OrderBy(x => x.ProductId).ToArray();
            var productIds = cartLines.Select(x => x.ProductId).ToArray();
            var products = await db.Products.AsNoTracking().Include(x => x.Images)
                .Where(x => productIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
            if (products.Count != productIds.Length || cartLines.Any(line =>
                    !products.TryGetValue(line.ProductId, out var product)
                    || product.Status != ProductStatus.Published
                    || product.SaleType != SaleType.InStock
                    || product.InStockOffer is null))
                return await RollbackFailure(CheckoutErrors.NotAvailable, transaction);

            var categoryIds = products.Values.Select(x => x.ProductCategoryId).Distinct().ToArray();
            var brandIds = products.Values.Select(x => x.BrandId).Distinct().ToArray();
            var universeIds = products.Values.Select(x => x.UniverseId).Distinct().ToArray();
            var categories = await db.ProductCategories.AsNoTracking().Where(x => categoryIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Code, cancellationToken);
            var brands = await db.Brands.AsNoTracking().Where(x => brandIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);
            var universes = await db.Universes.AsNoTracking().Where(x => universeIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.DisplayName, cancellationToken);

            var inventoryByProduct = new Dictionary<Guid, InventoryItem>();
            foreach (var productId in productIds.Order())
            {
                var rows = await db.InventoryItems.FromSqlInterpolated(
                    $"SELECT * FROM \"InventoryItems\" WHERE \"ProductId\" = {productId} FOR UPDATE")
                    .ToArrayAsync(cancellationToken);
                if (rows.Length != 1)
                    return await RollbackFailure(CheckoutErrors.StockInsufficient, transaction);
                inventoryByProduct.Add(productId, rows[0]);
            }

            if (cartLines.Any(line => line.Quantity > inventoryByProduct[line.ProductId].ReservableQuantity))
                return await RollbackFailure(CheckoutErrors.StockInsufficient, transaction);

            var expiresAt = request.NowUtc + PreOrderCapacityPolicy.ReservationLifetime;
            var definitions = new List<InStockCheckoutItemDefinition>(cartLines.Length);
            var reservations = new List<StockReservation>(cartLines.Length);
            foreach (var line in cartLines)
            {
                var product = products[line.ProductId];
                var inventory = inventoryByProduct[line.ProductId];
                var reservationId = Guid.NewGuid();
                var reservation = inventory.Reserve(reservationId, request.CheckoutAttemptId,
                    line.Quantity, request.NowUtc, expiresAt, "เริ่มชำระสินค้าพร้อมส่ง",
                    request.IdempotencyKey, inventory.Version, request.CustomerId);
                var primaryImage = product.Images.SingleOrDefault(x => x.IsPrimary)
                    ?? throw new InvalidOperationException("Published Product must have one primary image.");
                definitions.Add(new(Guid.NewGuid(), product.Id, inventory.Id, reservationId,
                    line.Quantity, product.DisplayName, product.EnglishName, product.Slug,
                    categories[product.ProductCategoryId], brands[product.BrandId],
                    universes[product.UniverseId], primaryImage.PublicRelativeUrl,
                    product.InStockOffer!.Price.Amount));
                reservations.Add(reservation);
            }

            var checkout = CheckoutAttempt.CreateInStock(request.CheckoutAttemptId,
                request.CustomerId, definitions, request.Address, request.IdempotencyKey,
                request.NowUtc, expiresAt);
            db.CheckoutAttempts.Add(checkout);
            db.StockReservations.AddRange(reservations);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<PreparedInStockCheckout>.Success(ToPrepared(checkout));
        }
        catch (InventoryRuleException exception) when (
            exception.Rule is InventoryRule.InsufficientReservableQuantity or InventoryRule.InsufficientOnHand)
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PreparedInStockCheckout>.Failure(CheckoutErrors.StockInsufficient);
        }
        catch (DbUpdateException exception) when (IsCheckoutConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            return Result<PreparedInStockCheckout>.Failure(CheckoutErrors.PaymentMismatch);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<PreparedInStockCheckout>> AttachProviderSessionAsync(
        string customerId, Guid checkoutAttemptId, string providerSessionId,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var checkout = await db.CheckoutAttempts.Include(x => x.Items).SingleOrDefaultAsync(x =>
            x.Id == checkoutAttemptId && x.CustomerId == customerId && x.SaleType == SaleType.InStock,
            cancellationToken);
        if (checkout is null) return Result<PreparedInStockCheckout>.Failure(CheckoutErrors.NotFound);
        try
        {
            checkout.AttachProviderSession(providerSessionId, timeProvider.GetUtcNow().ToUniversalTime());
            await db.SaveChangesAsync(cancellationToken);
            return Result<PreparedInStockCheckout>.Success(ToPrepared(checkout));
        }
        catch (InvalidOperationException)
        {
            return Result<PreparedInStockCheckout>.Failure(CheckoutErrors.PaymentMismatch);
        }
    }

    public async Task<Result<FulfilledInStockCheckout>> FulfillAsync(
        PaymentWebhookEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (!evidence.IsPaid || evidence.CheckoutAttemptId is null
            || string.IsNullOrWhiteSpace(evidence.PaymentReference))
            return Result<FulfilledInStockCheckout>.Failure(CheckoutErrors.PaymentMismatch);

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var rows = await db.CheckoutAttempts.FromSqlInterpolated(
                $"SELECT * FROM \"CheckoutAttempts\" WHERE \"Id\" = {evidence.CheckoutAttemptId.Value} FOR UPDATE")
                .ToArrayAsync(cancellationToken);
            var checkout = rows.SingleOrDefault();
            if (checkout is null || checkout.SaleType != SaleType.InStock)
                return await RollbackFailureFulfill(CheckoutErrors.NotFound, transaction);
            await db.Entry(checkout).Collection(x => x.Items).LoadAsync(cancellationToken);

            var expectedMinor = decimal.ToInt64(decimal.Round(checkout.PaymentAmount * 100m, 0));
            if (!string.Equals(checkout.ProviderSessionId, evidence.SessionId, StringComparison.Ordinal)
                || evidence.AmountTotalMinor != expectedMinor
                || !string.Equals(evidence.Currency, checkout.Currency, StringComparison.OrdinalIgnoreCase))
                return await RollbackFailureFulfill(CheckoutErrors.PaymentMismatch, transaction);

            var existingOrder = await db.Orders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CheckoutAttemptId == checkout.Id, cancellationToken);
            if (existingOrder is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<FulfilledInStockCheckout>.Success(new(existingOrder.Id, existingOrder.Number, false));
            }

            foreach (var item in checkout.Items.OrderBy(x => x.ResourceId))
            {
                var inventoryRows = await db.InventoryItems.FromSqlInterpolated(
                    $"SELECT * FROM \"InventoryItems\" WHERE \"Id\" = {item.ResourceId} FOR UPDATE")
                    .ToArrayAsync(cancellationToken);
                var inventory = inventoryRows.SingleOrDefault()
                    ?? throw new InvalidOperationException("Checkout inventory snapshot no longer exists.");
                var reservation = await db.StockReservations.SingleAsync(x =>
                    x.Id == item.ReservationId && x.CheckoutAttemptId == checkout.Id,
                    cancellationToken);
                var transition = inventory.ConsumeReservation(reservation, Guid.NewGuid(),
                    "Stripe ยืนยันการชำระเงิน", evidence.EventId, inventory.Version,
                    evidence.OccurredAtUtc.ToUniversalTime(), "stripe-webhook");
                if (transition.Movement is not null) db.StockMovements.Add(transition.Movement);
            }

            var orderId = Guid.NewGuid();
            var orderNumber = $"SY-{checkout.CreatedAtUtc:yyyyMMdd}-{checkout.Id.ToString("N")[..10].ToUpperInvariant()}";
            var order = Order.CreateInStock(orderId, orderNumber, checkout,
                evidence.OccurredAtUtc.ToUniversalTime());
            var payment = Payment.CreateFull(Guid.NewGuid(), orderId, checkout.Id,
                checkout.PaymentAmount, checkout.Currency, evidence.SessionId,
                evidence.PaymentReference!, evidence.EventId, evidence.OccurredAtUtc.ToUniversalTime());
            db.Orders.Add(order);
            db.Payments.Add(payment);
            checkout.Complete(evidence.OccurredAtUtc.ToUniversalTime());
            await RemovePurchasedCartQuantities(db, checkout, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result<FulfilledInStockCheckout>.Success(new(order.Id, order.Number, true));
        }
        catch (DbUpdateException exception) when (IsCheckoutConflict(exception))
        {
            await transaction.RollbackAsync(CancellationToken.None);
            await using var replay = await contextFactory.CreateDbContextAsync(cancellationToken);
            var existing = await replay.Orders.AsNoTracking()
                .SingleOrDefaultAsync(x => x.CheckoutAttemptId == evidence.CheckoutAttemptId, cancellationToken);
            return existing is null
                ? Result<FulfilledInStockCheckout>.Failure(CheckoutErrors.PaymentMismatch)
                : Result<FulfilledInStockCheckout>.Success(new(existing.Id, existing.Number, false));
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }

    public async Task<Result<InStockCheckoutStatusResult>> GetStatusAsync(
        string customerId, Guid checkoutAttemptId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var checkout = await db.CheckoutAttempts.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == checkoutAttemptId && x.CustomerId == customerId && x.SaleType == SaleType.InStock,
            cancellationToken);
        if (checkout is null) return Result<InStockCheckoutStatusResult>.Failure(CheckoutErrors.NotFound);
        var orderNumber = await db.Orders.AsNoTracking().Where(x => x.CheckoutAttemptId == checkout.Id)
            .Select(x => x.Number).SingleOrDefaultAsync(cancellationToken);
        return Result<InStockCheckoutStatusResult>.Success(new(checkout.Id,
            checkout.Status.ToString(), checkout.PaymentAmount, orderNumber));
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
            if (checkout is null || checkout.SaleType != SaleType.InStock)
                return await RollbackExpiredFailure(CheckoutErrors.NotFound, transaction);
            if (!string.Equals(checkout.ProviderSessionId, evidence.SessionId, StringComparison.Ordinal))
                return await RollbackExpiredFailure(CheckoutErrors.PaymentMismatch, transaction);
            if (checkout.Status is CheckoutAttemptStatus.Completed or CheckoutAttemptStatus.Expired)
            {
                await transaction.CommitAsync(cancellationToken);
                return Result<ExpiredCheckoutResult>.Success(new(checkout.Id, false));
            }
            await db.Entry(checkout).Collection(x => x.Items).LoadAsync(cancellationToken);
            var expiredAt = evidence.OccurredAtUtc.ToUniversalTime() < checkout.ExpiresAtUtc
                ? checkout.ExpiresAtUtc
                : evidence.OccurredAtUtc.ToUniversalTime();
            foreach (var item in checkout.Items.OrderBy(x => x.ResourceId))
            {
                var inventory = (await db.InventoryItems.FromSqlInterpolated(
                    $"SELECT * FROM \"InventoryItems\" WHERE \"Id\" = {item.ResourceId} FOR UPDATE")
                    .ToArrayAsync(cancellationToken)).Single();
                var reservation = await db.StockReservations.SingleAsync(x =>
                    x.Id == item.ReservationId && x.CheckoutAttemptId == checkout.Id,
                    cancellationToken);
                _ = inventory.ExpireReservation(reservation, "Stripe checkout หมดอายุ",
                    evidence.EventId, inventory.Version, expiredAt, "stripe-webhook");
            }
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

    private static async Task RemovePurchasedCartQuantities(
        ApplicationDbContext db, CheckoutAttempt checkout, CancellationToken cancellationToken)
    {
        var carts = await db.Carts.FromSqlInterpolated(
            $"SELECT * FROM \"Carts\" WHERE \"CustomerId\" = {checkout.CustomerId} FOR UPDATE")
            .ToArrayAsync(cancellationToken);
        if (carts.Length == 0) return;
        var cart = carts.Single();
        await db.Entry(cart).Collection(x => x.Items).LoadAsync(cancellationToken);
        var changedAt = checkout.CompletedAtUtc!.Value;
        foreach (var purchased in checkout.Items)
        {
            var current = cart.Items.SingleOrDefault(x => x.ProductId == purchased.ProductId);
            if (current is null) continue;
            if (current.Quantity > purchased.Quantity)
                cart.SetQuantity(current.ProductId, current.Quantity - purchased.Quantity, cart.Version, changedAt);
            else
                cart.Remove(current.ProductId, cart.Version, changedAt);
        }
    }

    private static PreparedInStockCheckout ToPrepared(CheckoutAttempt checkout) => new(
        checkout.Id, checkout.CustomerId, checkout.IdempotencyKey,
        checkout.Items.Select(item => new PreparedInStockCheckoutItem(item.ProductId,
            item.DisplayName, item.PrimaryImageUrl, item.Quantity, item.UnitPrice,
            item.LinePaymentAmount)).ToArray(), checkout.ShippingAmount, checkout.PaymentAmount,
        checkout.Currency, checkout.ExpiresAtUtc, checkout.ProviderSessionId);

    private static async Task<Result<PreparedInStockCheckout>> RollbackFailure(
        Error error, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return Result<PreparedInStockCheckout>.Failure(error);
    }

    private static async Task<Result<FulfilledInStockCheckout>> RollbackFailureFulfill(
        Error error, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return Result<FulfilledInStockCheckout>.Failure(error);
    }

    private static async Task<Result<ExpiredCheckoutResult>> RollbackExpiredFailure(
        Error error, Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction)
    {
        await transaction.RollbackAsync(CancellationToken.None);
        return Result<ExpiredCheckoutResult>.Failure(error);
    }

    private static bool IsCheckoutConflict(DbUpdateException exception) =>
        exception.InnerException is PostgresException postgres
        && postgres.SqlState is PostgresErrorCodes.UniqueViolation
            or PostgresErrorCodes.SerializationFailure;
}
