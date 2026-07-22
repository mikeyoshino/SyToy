using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using ToyStore.Application.Notifications;
using ToyStore.Domain.Notifications;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.Infrastructure.Notifications;

internal sealed partial class TelegramOrderPlacedNotificationDispatcher(
    IDbContextFactory<ApplicationDbContext> contextFactory,
    TelegramBotClient botClient,
    IOptions<TelegramNotificationOptions> options,
    TimeProvider timeProvider,
    ILogger<TelegramOrderPlacedNotificationDispatcher> logger)
    : IOrderPlacedNotificationDispatcher
{
    private static readonly TimeSpan AbandonedAttemptAfter = TimeSpan.FromMinutes(5);

    public async Task DispatchAsync(
        Guid orderId,
        string orderNumber,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Enabled) return;

        var delivery = await EnsureDeliveryAsync(
            orderId,
            orderNumber,
            settings,
            cancellationToken);
        if (!await TryClaimAsync(delivery.Id, cancellationToken)) return;

        TelegramSendResult sendResult;
        try
        {
            sendResult = await botClient.SendMessageAsync(
                settings.BotToken,
                settings.ChatId,
                delivery.Payload,
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await RecordFailureAsync(delivery.Id, "telegram-cancelled", CancellationToken.None);
            throw;
        }
        catch (Exception exception)
        {
            sendResult = new(false, $"telegram-exception:{exception.GetType().Name}");
        }

        await RecordResultAsync(delivery.Id, sendResult, cancellationToken);
        if (!sendResult.IsSuccess)
        {
            LogDeliveryFailure(
                logger,
                delivery.Id,
                sendResult.SafeProviderResponse);
        }
    }

    private async Task<NotificationDelivery> EnsureDeliveryAsync(
        Guid orderId,
        string orderNumber,
        TelegramNotificationOptions settings,
        CancellationToken cancellationToken)
    {
        var idempotencyKey = $"telegram:order-placed:{orderId:N}";
        await using (var read = await contextFactory.CreateDbContextAsync(cancellationToken))
        {
            var existing = await read.NotificationDeliveries.AsNoTracking()
                .SingleOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey, cancellationToken);
            if (existing is not null) return existing;
        }

        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(
            x => x.Id == orderId && x.Number == orderNumber,
            cancellationToken)
            ?? throw new InvalidOperationException("The committed Order could not be loaded for notification.");
        var amountReceived = await db.Payments.AsNoTracking()
            .Where(x => x.OrderId == orderId
                && (x.Purpose == PaymentPurpose.Full || x.Purpose == PaymentPurpose.Deposit))
            .Select(x => x.Amount)
            .SingleAsync(cancellationToken);
        var message = BuildMessage(
            order.Number,
            order.SaleType,
            amountReceived,
            settings.AdminBaseUrl);
        var created = NotificationDelivery.Create(
            Guid.NewGuid(),
            order.Id,
            "OrderPlaced.Telegram",
            settings.ChatId,
            idempotencyKey,
            message,
            timeProvider.GetUtcNow().ToUniversalTime());
        db.NotificationDeliveries.Add(created);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return created;
        }
        catch (DbUpdateException exception) when (
            exception.InnerException is PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
            })
        {
            await using var replay = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await replay.NotificationDeliveries.AsNoTracking().SingleAsync(
                x => x.IdempotencyKey == idempotencyKey,
                cancellationToken);
        }
    }

    private async Task<bool> TryClaimAsync(Guid deliveryId, CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.ReadCommitted,
            cancellationToken);
        var delivery = (await db.NotificationDeliveries.FromSqlInterpolated(
                $"SELECT * FROM \"NotificationDeliveries\" WHERE \"Id\" = {deliveryId} FOR UPDATE")
            .ToArrayAsync(cancellationToken)).Single();
        var claimed = delivery.TryBeginAttempt(
            timeProvider.GetUtcNow().ToUniversalTime(),
            AbandonedAttemptAfter);
        if (claimed) await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return claimed;
    }

    private async Task RecordResultAsync(
        Guid deliveryId,
        TelegramSendResult result,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var delivery = await db.NotificationDeliveries.SingleAsync(
            x => x.Id == deliveryId,
            cancellationToken);
        if (result.IsSuccess)
            delivery.MarkSent(result.SafeProviderResponse, timeProvider.GetUtcNow().ToUniversalTime());
        else
            delivery.MarkFailed(result.SafeProviderResponse);
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task RecordFailureAsync(
        Guid deliveryId,
        string safeResponse,
        CancellationToken cancellationToken)
    {
        await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
        var delivery = await db.NotificationDeliveries.SingleAsync(
            x => x.Id == deliveryId,
            cancellationToken);
        delivery.MarkFailed(safeResponse);
        await db.SaveChangesAsync(cancellationToken);
    }

    internal static string BuildMessage(
        string orderNumber,
        SaleType saleType,
        decimal amountReceived,
        string adminBaseUrl)
    {
        var saleTypeLabel = saleType == SaleType.PreOrder ? "พรีออเดอร์" : "สินค้าพร้อมส่ง";
        var baseUri = new Uri(adminBaseUrl.TrimEnd('/') + "/", UriKind.Absolute);
        var adminUri = new Uri(baseUri, $"admin/orders/{Uri.EscapeDataString(orderNumber)}");
        return string.Join('\n',
            "🛒 มีคำสั่งซื้อใหม่",
            $"เลขที่: {orderNumber}",
            $"ประเภท: {saleTypeLabel}",
            $"ยอดรับชำระ: ฿{amountReceived.ToString("N2", CultureInfo.InvariantCulture)} THB",
            $"จัดการคำสั่งซื้อ: {adminUri}");
    }

    [LoggerMessage(
        EventId = 7303,
        Level = LogLevel.Warning,
        Message = "Telegram notification delivery {DeliveryId} failed with {ProviderResult}.")]
    private static partial void LogDeliveryFailure(
        ILogger logger,
        Guid deliveryId,
        string providerResult);
}
