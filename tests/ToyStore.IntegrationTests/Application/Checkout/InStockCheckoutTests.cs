using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Checkout;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Checkout;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InStockCheckoutTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ExpiredStripeSessionReleasesAllStockExactlyOnceWithoutOrder()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var now = DateTimeOffset.UtcNow;
        var seeded = await SeedAsync(factory, now);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
        var checkoutId = Guid.NewGuid();
        var prepared = await store.PrepareAsync(new(checkoutId, seeded.CustomerId,
            ShippingAddressSnapshot.Create("ผู้รับสินค้า", "0812345678", "99 ถนนสุขุมวิท",
                "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
            Guid.NewGuid().ToString("N"), now), TestContext.Current.CancellationToken);
        Assert.True(prepared.IsSuccess);
        Assert.True((await store.AttachProviderSessionAsync(seeded.CustomerId, checkoutId,
            "cs_expired", TestContext.Current.CancellationToken)).IsSuccess);
        var evidence = new PaymentWebhookEvidence("evt_expired", "checkout.session.expired",
            "cs_expired", checkoutId, null, 0, "thb", false,
            prepared.Value.ExpiresAtUtc, "instock_full");

        var expired = await store.ExpireAsync(evidence, TestContext.Current.CancellationToken);
        var replay = await store.ExpireAsync(evidence, TestContext.Current.CancellationToken);

        Assert.True(expired.IsSuccess);
        Assert.True(expired.Value.Changed);
        Assert.False(replay.Value.Changed);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventory = await db.InventoryItems.SingleAsync(TestContext.Current.CancellationToken);
        var reservation = await db.StockReservations.SingleAsync(TestContext.Current.CancellationToken);
        var checkout = await db.CheckoutAttempts.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.HeldQuantity);
        Assert.Equal(StockReservationStatus.Expired, reservation.Status);
        Assert.Equal(CheckoutAttemptStatus.Expired, checkout.Status);
        Assert.Empty(db.Orders);
        Assert.Empty(db.Payments);
    }

    [Fact]
    public async Task ConcurrentCheckoutsForLastStockHaveExactlyOneWinner()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var now = DateTimeOffset.UtcNow;
        var seeded = await SeedAsync(factory, now);
        var secondCustomer = Guid.NewGuid().ToString("N");
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var db = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await db.Products.SingleAsync(x => x.Id == seeded.ProductId,
                TestContext.Current.CancellationToken);
            var email = $"{secondCustomer}@example.test";
            db.Users.Add(new ApplicationUser
            {
                Id = secondCustomer, UserName = email, NormalizedUserName = email.ToUpperInvariant(),
                Email = email, NormalizedEmail = email.ToUpperInvariant(),
                SecurityStamp = Guid.NewGuid().ToString("N"),
            });
            var cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), secondCustomer, now);
            cart.Add(product, 2, cart.Version, now);
            db.Carts.Add(cart);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
        var address = ShippingAddressSnapshot.Create("ผู้รับสินค้า", "0812345678",
            "99 ถนนสุขุมวิท", "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110");
        async Task<Result<PreparedInStockCheckout>> Prepare(string customerId) =>
            await store.PrepareAsync(new(Guid.NewGuid(), customerId, address,
                Guid.NewGuid().ToString("N"), now), TestContext.Current.CancellationToken);

        var results = await Task.WhenAll(Prepare(seeded.CustomerId), Prepare(secondCustomer));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(CheckoutErrors.StockInsufficient,
            Assert.Single(results, result => result.IsFailure).Error);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await verification.CheckoutAttempts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await verification.StockReservations.CountAsync(TestContext.Current.CancellationToken));
        var inventory = await verification.InventoryItems.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, inventory.HeldQuantity);
        Assert.Equal(2, inventory.OnHandQuantity);
    }

    [Fact]
    public async Task PrepareAndVerifiedFulfillmentReserveConsumeAndCreateExactlyOnce()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var now = DateTimeOffset.UtcNow;
        var seeded = await SeedAsync(factory, now);
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
        var checkoutId = Guid.NewGuid();
        var prepared = await store.PrepareAsync(new(checkoutId, seeded.CustomerId,
            ShippingAddressSnapshot.Create("ผู้รับสินค้า", "0812345678", "99 ถนนสุขุมวิท",
                "คลองเตย", "คลองเตย", "กรุงเทพมหานคร", "10110"),
            Guid.NewGuid().ToString("N"), now), TestContext.Current.CancellationToken);

        Assert.True(prepared.IsSuccess);
        Assert.Equal(2000m, prepared.Value.PaymentAmount);
        Assert.Equal(2, Assert.Single(prepared.Value.Items).Quantity);
        var attached = await store.AttachProviderSessionAsync(seeded.CustomerId, checkoutId,
            "cs_test_instock", TestContext.Current.CancellationToken);
        Assert.True(attached.IsSuccess);
        var evidence = new PaymentWebhookEvidence("evt_instock", "checkout.session.completed",
            "cs_test_instock", checkoutId, "pi_instock", 200000, "thb", true,
            now.AddSeconds(10), "instock_full");

        var fulfilled = await store.FulfillAsync(evidence, TestContext.Current.CancellationToken);
        var replay = await store.FulfillAsync(evidence, TestContext.Current.CancellationToken);
        var mismatchedReplay = await store.FulfillAsync(evidence with { SessionId = "cs_wrong" },
            TestContext.Current.CancellationToken);

        Assert.True(fulfilled.IsSuccess);
        Assert.True(fulfilled.Value.Changed);
        Assert.True(replay.IsSuccess);
        Assert.False(replay.Value.Changed);
        Assert.Equal(CheckoutErrors.PaymentMismatch, mismatchedReplay.Error);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var db = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventory = await db.InventoryItems.SingleAsync(TestContext.Current.CancellationToken);
        var reservation = await db.StockReservations.SingleAsync(TestContext.Current.CancellationToken);
        var order = await db.Orders.Include(x => x.Items).SingleAsync(TestContext.Current.CancellationToken);
        var payment = await db.Payments.SingleAsync(TestContext.Current.CancellationToken);
        var cart = await db.Carts.Include(x => x.Items).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(0, inventory.OnHandQuantity);
        Assert.Equal(0, inventory.HeldQuantity);
        Assert.Equal(StockReservationStatus.Consumed, reservation.Status);
        Assert.Equal(PaymentStatus.Paid, order.PaymentStatus);
        Assert.Equal(FulfillmentStatus.ReadyToShip, order.FulfillmentStatus);
        Assert.Equal(PaymentPurpose.Full, payment.Purpose);
        Assert.Single(order.Items);
        Assert.Empty(cart.Items);
        Assert.Equal(1, await db.Orders.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.Payments.CountAsync(TestContext.Current.CancellationToken));
    }

    private static async Task<(string CustomerId, Guid ProductId)> SeedAsync(
        ToyStoreWebApplicationFactory factory, DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var customerId = Guid.NewGuid().ToString("N");
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
        var brand = Brand.Create(Guid.NewGuid(), "แบรนด์ Checkout", "Checkout Brand",
            CatalogSlug.Create("checkout-brand"), now.AddMinutes(-2), "test");
        var product = Product.CreateInStock(Guid.NewGuid(), "สินค้า Checkout", "Checkout Product",
            "รายละเอียด", "checkout-product", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.MarvelUniverse, InStockOffer.Create(Money.Create(1000)),
            [new ProductImageDefinition(Guid.NewGuid(), "checkout/main.webp", "/media/checkout.webp", "สินค้า Checkout")],
            [], now.AddMinutes(-1), "test");
        product.Publish(product.Version, now, "test");
        var inventory = InventoryItem.Create(Guid.NewGuid(), product.Id, Guid.NewGuid(), 2,
            "สต็อกเริ่มต้น", "checkout-test", now, "test");
        var cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), customerId, now);
        cart.Add(product, 2, cart.Version, now);
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.InventoryItems.Add(inventory.Item);
        db.StockMovements.Add(inventory.InitialMovement);
        db.Carts.Add(cart);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (customerId, product.Id);
    }
}
