using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Checkout;
using ToyStore.Application.Orders;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Orders;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CustomerOrderReaderTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task HistoryAndDetailOnlyReturnOrdersOwnedByAuthorizedCustomer()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var now = DateTimeOffset.UtcNow;
        var seeded = await SeedAsync(factory, now);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var store = scope.ServiceProvider.GetRequiredService<IInStockCheckoutStore>();
            var checkoutId = Guid.NewGuid();
            var prepared = await store.PrepareAsync(new(
                checkoutId,
                seeded.OwnerId,
                ShippingAddressSnapshot.Create(
                    "ผู้รับตาม Snapshot",
                    "0812345678",
                    "99 ถนนสุขุมวิท",
                    "คลองเตย",
                    "คลองเตย",
                    "กรุงเทพมหานคร",
                    "10110"),
                Guid.NewGuid().ToString("N"),
                now), TestContext.Current.CancellationToken);
            Assert.True(prepared.IsSuccess);
            Assert.True((await store.AttachProviderSessionAsync(
                seeded.OwnerId,
                checkoutId,
                "cs_customer_order_reader",
                TestContext.Current.CancellationToken)).IsSuccess);
            var fulfilled = await store.FulfillAsync(new PaymentWebhookEvidence(
                "evt_customer_order_reader",
                "checkout.session.completed",
                "cs_customer_order_reader",
                checkoutId,
                "pi_customer_order_reader",
                125000,
                "thb",
                true,
                now.AddSeconds(1),
                "instock_full"), TestContext.Current.CancellationToken);
            Assert.True(fulfilled.IsSuccess);
        }

        await using var readScope = factory.Services.CreateAsyncScope();
        var reader = readScope.ServiceProvider.GetRequiredService<ICustomerOrderReader>();
        var ownerHistory = await reader.ListAsync(
            seeded.OwnerId, 1, 12, null, TestContext.Current.CancellationToken);
        var strangerHistory = await reader.ListAsync(
            seeded.StrangerId, 1, 12, null, TestContext.Current.CancellationToken);
        var numberSearch = await reader.ListAsync(
            seeded.OwnerId, 1, 12, ownerHistory.Items.Single().Number[^6..],
            TestContext.Current.CancellationToken);
        var productSearch = await reader.ListAsync(
            seeded.OwnerId, 1, 12, "Order History",
            TestContext.Current.CancellationToken);
        var wildcardSearch = await reader.ListAsync(
            seeded.OwnerId, 1, 12, "%_",
            TestContext.Current.CancellationToken);
        var summary = Assert.Single(ownerHistory.Items);
        var ownerDetail = await reader.GetAsync(
            seeded.OwnerId, summary.Number, TestContext.Current.CancellationToken);
        var strangerDetail = await reader.GetAsync(
            seeded.StrangerId, summary.Number, TestContext.Current.CancellationToken);
        var adminReader = readScope.ServiceProvider.GetRequiredService<IAdminOrderReader>();
        var adminPage = await adminReader.ListAsync(new AdminOrderReadRequest(
            "Order History",
            AdminOrderSaleType.InStock,
            AdminOrderPaymentStatus.Paid,
            AdminOrderFulfillmentStatus.ReadyToShip,
            now.AddHours(-1).ToUniversalTime(),
            now.AddHours(1).ToUniversalTime(),
            1,
            20), TestContext.Current.CancellationToken);
        var adminDetail = await adminReader.GetAsync(
            summary.Number, TestContext.Current.CancellationToken);

        Assert.Equal(1, ownerHistory.TotalCount);
        Assert.Empty(strangerHistory.Items);
        Assert.Equal(0, strangerHistory.TotalCount);
        Assert.Single(numberSearch.Items);
        Assert.Single(productSearch.Items);
        Assert.Empty(wildcardSearch.Items);
        Assert.NotNull(ownerDetail);
        Assert.Null(strangerDetail);
        Assert.Equal("ผู้รับตาม Snapshot", ownerDetail.Address.RecipientName);
        Assert.Equal("สินค้า Order History", Assert.Single(ownerDetail.Items).DisplayName);
        Assert.Single(adminPage.Items);
        Assert.NotNull(adminDetail);
        Assert.Equal(seeded.OwnerId + "@example.test", adminDetail.CustomerEmail);
        Assert.Equal("pi_customer_order_reader", Assert.Single(adminDetail.Payments).ProviderPaymentReference);

        var shipmentStore = readScope.ServiceProvider.GetRequiredService<IShipmentMutationStore>();
        var operationId = Guid.NewGuid();
        var shipmentRequest = new ShipmentMutationRequest(summary.Number, ShippingCarrier.ThailandPost,
            "EF123456789TH", null, adminDetail.Version, operationId, "admin-test",
            now.AddSeconds(2).ToUniversalTime());
        var shipped = await shipmentStore.CreateAsync(shipmentRequest, TestContext.Current.CancellationToken);
        var replay = await shipmentStore.CreateAsync(shipmentRequest, TestContext.Current.CancellationToken);
        var conflict = await shipmentStore.CreateAsync(shipmentRequest with { OperationId = Guid.NewGuid() },
            TestContext.Current.CancellationToken);
        var shippedDetail = await adminReader.GetAsync(summary.Number, TestContext.Current.CancellationToken);
        var shippedCustomerDetail = await reader.GetAsync(seeded.OwnerId, summary.Number, TestContext.Current.CancellationToken);
        var trackingSearch = await adminReader.ListAsync(new AdminOrderReadRequest("EF123456789TH", null, null,
            AdminOrderFulfillmentStatus.Shipped, null, null, 1, 20), TestContext.Current.CancellationToken);

        Assert.True(shipped.IsSuccess); Assert.True(shipped.Value.Changed);
        Assert.True(replay.IsSuccess); Assert.False(replay.Value.Changed);
        Assert.True(conflict.IsFailure); Assert.Equal(AdminOrderErrors.ShipmentConflict, conflict.Error);
        Assert.Equal(AdminOrderFulfillmentStatus.Shipped, shippedDetail!.FulfillmentStatus);
        Assert.NotNull(shippedDetail.Shipment); Assert.Single(shippedDetail.AuditEvents);
        Assert.NotNull(shippedCustomerDetail!.Shipment); Assert.Single(trackingSearch.Items);
    }

    private static async Task<(string OwnerId, string StrangerId)> SeedAsync(
        ToyStoreWebApplicationFactory factory,
        DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var ownerId = Guid.NewGuid().ToString("N");
        var strangerId = Guid.NewGuid().ToString("N");
        db.Users.AddRange(CreateUser(ownerId), CreateUser(strangerId));
        var brand = Brand.Create(
            Guid.NewGuid(),
            "แบรนด์ Order History",
            "Order History Brand",
            CatalogSlug.Create($"order-history-brand-{Guid.NewGuid():N}"),
            now.AddMinutes(-2),
            "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้า Order History",
            "Order History Product",
            "รายละเอียดเดิมของสินค้า",
            $"order-history-product-{Guid.NewGuid():N}",
            CatalogSeedIds.ArtToyCategory,
            brand.Id,
            CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1250)),
            [new ProductImageDefinition(
                Guid.NewGuid(),
                "order-history/main.webp",
                "/media/order-history.webp",
                "สินค้า Order History")],
            [],
            now.AddMinutes(-1),
            "test");
        product.Publish(product.Version, now, "test");
        var inventory = InventoryItem.Create(
            Guid.NewGuid(),
            product.Id,
            Guid.NewGuid(),
            1,
            "สต็อกทดสอบ Order History",
            "order-history-test",
            now,
            "test");
        var cart = ToyStore.Domain.Carts.Cart.Create(Guid.NewGuid(), ownerId, now);
        cart.Add(product, 1, cart.Version, now);
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.InventoryItems.Add(inventory.Item);
        db.StockMovements.Add(inventory.InitialMovement);
        db.Carts.Add(cart);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return (ownerId, strangerId);
    }

    private static ApplicationUser CreateUser(string id)
    {
        var email = $"{id}@example.test";
        return new ApplicationUser
        {
            Id = id,
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            SecurityStamp = Guid.NewGuid().ToString("N"),
        };
    }
}
