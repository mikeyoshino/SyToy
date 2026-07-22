using System.Net;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToyStore.Application.Checkout;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Notifications;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Notifications;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Notifications;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class TelegramOrderNotificationTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task DuplicateDispatchSendsOneDurableNotificationAfterOrderCommit()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var fulfilled = await CreatePaidOrderAsync(factory);
        var handler = new StubHandler(HttpStatusCode.OK,
            "{\"ok\":true,\"result\":{\"message_id\":77}}");
        using var dispatcher = CreateDispatcher(factory, handler);

        await dispatcher.Value.DispatchAsync(
            fulfilled.OrderId, fulfilled.OrderNumber, TestContext.Current.CancellationToken);
        await dispatcher.Value.DispatchAsync(
            fulfilled.OrderId, fulfilled.OrderNumber, TestContext.Current.CancellationToken);

        Assert.Equal(1, handler.SendCount);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var delivery = await db.NotificationDeliveries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NotificationDeliveryStatus.Sent, delivery.Status);
        Assert.Equal(1, delivery.Attempts);
        Assert.Equal("telegram-message:77", delivery.SafeProviderResponse);
        Assert.Equal(1, await db.Orders.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Payments.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ProviderFailureIsPersistedWithoutRollingBackPaidOrder()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var fulfilled = await CreatePaidOrderAsync(factory);
        var handler = new StubHandler(HttpStatusCode.BadGateway,
            "{\"ok\":false,\"error_code\":502,\"description\":\"test\"}");
        using var dispatcher = CreateDispatcher(factory, handler);

        await dispatcher.Value.DispatchAsync(
            fulfilled.OrderId, fulfilled.OrderNumber, TestContext.Current.CancellationToken);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var delivery = await db.NotificationDeliveries.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(NotificationDeliveryStatus.Failed, delivery.Status);
        Assert.Equal("telegram-error:502", delivery.SafeProviderResponse);
        Assert.Equal(1, await db.Orders.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Payments.CountAsync(TestContext.Current.CancellationToken));
    }

    private static DisposableDispatcher CreateDispatcher(
        ToyStoreWebApplicationFactory factory,
        HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.telegram.org") };
        var botClient = new TelegramBotClient(httpClient);
        var contextFactory = factory.Services.GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var dispatcher = new TelegramOrderPlacedNotificationDispatcher(
            contextFactory,
            botClient,
            Options.Create(new TelegramNotificationOptions
            {
                Enabled = true,
                BotToken = "123456:test_token",
                ChatId = "-100123456",
                AdminBaseUrl = "https://sytoys.shop",
            }),
            TimeProvider.System,
            NullLogger<TelegramOrderPlacedNotificationDispatcher>.Instance);
        return new(dispatcher, botClient);
    }

    private static async Task<FulfilledInStockCheckout> CreatePaidOrderAsync(
        ToyStoreWebApplicationFactory factory)
    {
        var now = DateTimeOffset.UtcNow;
        string customerId;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            customerId = Guid.NewGuid().ToString("N");
            var email = $"{customerId}@example.test";
            db.Users.Add(new ApplicationUser
            {
                Id = customerId,
                UserName = email,
                NormalizedUserName = email.ToUpperInvariant(),
                Email = email,
                NormalizedEmail = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString("N"),
            });
            var brand = Brand.Create(Guid.NewGuid(), "แบรนด์แจ้งเตือน", "Notification Brand",
                CatalogSlug.Create("notification-brand"), now.AddMinutes(-2), "test");
            var product = Product.CreateInStock(Guid.NewGuid(), "สินค้าแจ้งเตือน", "Notification Product",
                "รายละเอียด", "notification-product", CatalogSeedIds.ArtToyCategory, brand.Id,
                CatalogSeedIds.MarvelUniverse, InStockOffer.Create(Money.Create(1000)),
                [new ProductImageDefinition(Guid.NewGuid(), "notify/main.webp", "/media/notify.webp", "สินค้า")],
                [], now.AddMinutes(-1), "test");
            product.Publish(product.Version, now, "test");
            var inventory = InventoryItem.Create(Guid.NewGuid(), product.Id, Guid.NewGuid(), 1,
                "สต็อกเริ่มต้น", "notification-test", now, "test");
            var cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), customerId, now);
            cart.Add(product, 1, cart.Version, now);
            db.Brands.Add(brand);
            db.Products.Add(product);
            db.InventoryItems.Add(inventory.Item);
            db.StockMovements.Add(inventory.InitialMovement);
            db.Carts.Add(cart);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
        var checkoutId = Guid.NewGuid();
        var prepared = await store.PrepareAsync(new(
            checkoutId,
            customerId,
            ShippingAddressSnapshot.Create("ผู้รับสินค้า", "0812345678", "99 ถนนสุขุมวิท",
                "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
            Guid.NewGuid().ToString("N"),
            now), TestContext.Current.CancellationToken);
        Assert.True(prepared.IsSuccess);
        Assert.True((await store.AttachProviderSessionAsync(
            customerId, checkoutId, "cs_notification", TestContext.Current.CancellationToken)).IsSuccess);
        var result = await store.FulfillAsync(new PaymentWebhookEvidence(
            "evt_notification", "checkout.session.completed", "cs_notification", checkoutId,
            "pi_notification", 100000, "thb", true, now.AddSeconds(10), "instock_full"),
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return result.Value;
    }

    private sealed class StubHandler(HttpStatusCode statusCode, string body) : HttpMessageHandler
    {
        public int SendCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            SendCount++;
            return Task.FromResult(new HttpResponseMessage(statusCode)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            });
        }
    }

    private sealed class DisposableDispatcher(
        TelegramOrderPlacedNotificationDispatcher value,
        TelegramBotClient botClient) : IDisposable
    {
        public TelegramOrderPlacedNotificationDispatcher Value { get; } = value;
        public void Dispose() => botClient.Dispose();
    }
}
