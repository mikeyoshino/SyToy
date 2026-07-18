using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Inventory;
using ToyStore.Application.Inventory.GetInventoryAvailability;
using ToyStore.Application.Inventory.ListStockMovements;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Inventory;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InventoryReadQueryTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task AvailabilityAggregatesEveryPhysicalActiveHoldAndOnlyFutureEffectiveHold()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAvailabilityScenarioAsync(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new GetInventoryAvailabilityHandler(
            scope.ServiceProvider.GetRequiredService<IInventoryReadStore>(),
            new FixedTimeProvider());

        var result = await handler.Handle(
            new GetInventoryAvailabilityQuery(seeded.InventoryId, seeded.ProductId),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(10, result.Value.OnHandQuantity);
        Assert.Equal(4, result.Value.PhysicalHeldQuantity);
        Assert.Equal(6, result.Value.ReservableQuantity);
        Assert.Equal(1, result.Value.EffectiveReservedQuantity);
        Assert.Equal(9, result.Value.CustomerAvailableQuantity);

        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "InventoryItems"
            SET "HeldQuantity" = 3
            WHERE "Id" = {seeded.InventoryId}
            """,
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new GetInventoryAvailabilityQuery(seeded.InventoryId, seeded.ProductId),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MovementHistoryIsScopedDeterministicImmutableAndCanonicalPaged()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedMovementScenarioAsync(factory, "history");
        _ = await SeedMovementScenarioAsync(factory, "other");
        await using var scope = factory.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IInventoryReadStore>();
        var handler = new ListStockMovementsHandler(store);

        var all = await handler.Handle(
            new ListStockMovementsQuery(
                seeded.InventoryId, seeded.ProductId, Page: 1, PageSize: 100),
            TestContext.Current.CancellationToken);
        var clamped = await handler.Handle(
            new ListStockMovementsQuery(
                seeded.InventoryId, seeded.ProductId, Page: 999, PageSize: 2),
            TestContext.Current.CancellationToken);

        Assert.True(all.IsSuccess);
        Assert.Equal(4, all.Value.TotalCount);
        Assert.All(all.Value.Items, item =>
        {
            Assert.Equal(seeded.InventoryId, item.InventoryItemId);
            Assert.Equal(seeded.ProductId, item.ProductId);
        });
        Assert.Equal(
            all.Value.Items
                .OrderByDescending(item => item.OccurredAtUtc)
                .ThenByDescending(item => item.Id)
                .Select(item => item.Id),
            all.Value.Items.Select(item => item.Id));
        Assert.Equal(2, clamped.Value.PageNumber);
        Assert.Equal(2, clamped.Value.Items.Count);
        Assert.Equal(
            InventoryErrors.NotFound,
            (await handler.Handle(
                new ListStockMovementsQuery(
                    seeded.InventoryId, Guid.NewGuid()),
                TestContext.Current.CancellationToken)).Error);
    }

    private static readonly DateTimeOffset NowUtc =
        new(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<SeededInventory> SeedAvailabilityScenarioAsync(
        ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = CreateProduct("availability");
        var creation = InventoryItem.Create(
            Guid.NewGuid(), product.Product.Id, Guid.NewGuid(), 10,
            "สินค้าเริ่มต้น", "availability-initial", NowUtc.AddHours(-4), "test");
        var item = creation.Item;
        var past = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, NowUtc.AddHours(-3),
            NowUtc.AddHours(-2), "รอชำระ", "past", item.Version, "system");
        var exact = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 2, NowUtc.AddHours(-2),
            NowUtc, "รอชำระ", "exact", item.Version, "system");
        var future = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, NowUtc.AddHours(-1),
            NowUtc.AddHours(1), "รอชำระ", "future", item.Version, "system");
        var terminal = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 2, NowUtc.AddMinutes(-50),
            NowUtc.AddHours(1), "รอชำระ", "terminal", item.Version, "system");
        _ = item.ReleaseReservation(
            terminal, "ยกเลิก", "terminal-release", item.Version,
            NowUtc.AddMinutes(-40), "system");
        db.Brands.Add(product.Brand);
        db.Products.Add(product.Product);
        db.InventoryItems.Add(item);
        db.StockMovements.Add(creation.InitialMovement);
        db.StockReservations.AddRange(past, exact, future, terminal);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededInventory(item.Id, item.ProductId);
    }

    private static async Task<SeededInventory> SeedMovementScenarioAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = CreateProduct(suffix);
        var creation = InventoryItem.Create(
            Guid.NewGuid(), product.Product.Id, Guid.NewGuid(), 0,
            "สินค้าเริ่มต้น", $"{suffix}-initial", NowUtc.AddHours(-4), "test");
        var item = creation.Item;
        var first = item.ReceiveStock(
            Guid.NewGuid(), 1, "รับสินค้า", $"{suffix}-first", item.Version,
            NowUtc.AddHours(-1), "admin");
        var second = item.ReceiveStock(
            Guid.NewGuid(), 1, "รับสินค้า", $"{suffix}-second", item.Version,
            NowUtc.AddHours(-1), "admin");
        var third = item.AdjustStock(
            Guid.NewGuid(), 1, "ปรับสต็อก", $"{suffix}-third", item.Version,
            NowUtc, "admin");
        db.Brands.Add(product.Brand);
        db.Products.Add(product.Product);
        db.InventoryItems.Add(item);
        db.StockMovements.AddRange(creation.InitialMovement, first, second, third);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededInventory(item.Id, item.ProductId);
    }

    private static SeededProduct CreateProduct(string suffix)
    {
        var brand = Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), NowUtc.AddHours(-4), "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
            $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, InStockOffer.Create(Money.Create(100)),
            NowUtc.AddHours(-4), "test");
        return new SeededProduct(brand, product);
    }

    private sealed record SeededInventory(Guid InventoryId, Guid ProductId);

    private sealed record SeededProduct(Brand Brand, Product Product);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => NowUtc;
    }
}
