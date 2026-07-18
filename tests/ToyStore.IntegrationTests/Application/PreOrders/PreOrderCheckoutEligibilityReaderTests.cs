using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Application.PreOrders.GetPreOrderCheckoutEligibility;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.PreOrders;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PreOrderCheckoutEligibilityReaderTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset CreatedAt =
        new(2026, 7, 1, 0, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset CheckedAt =
        new(2026, 7, 17, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostgreSqlProjectionCountsExpiredActiveAndConsumedButExcludesTerminalWithoutWrites()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedPublishedAsync(factory, "projection", withReservations: true);
        var before = await CountsAsync(factory);

        var first = await QueryAsync(factory, seeded.ProductId, 1, CheckedAt);
        var retry = await QueryAsync(factory, seeded.ProductId, 1, CheckedAt);
        var after = await CountsAsync(factory);

        Assert.True(first.IsSuccess);
        Assert.Equal(first.Value, retry.Value);
        Assert.Equal(seeded.CapacityId, first.Value.CapacityId);
        Assert.Equal(3, first.Value.RemainingCapacity);
        Assert.Equal(2, first.Value.CustomerAllocatedQuantity);
        Assert.Equal(1, first.Value.CustomerRemainingAllowance);
        Assert.Equal(1000, first.Value.FullPrice);
        Assert.Equal(200, first.Value.DepositAmount);
        Assert.Equal(800, first.Value.BalanceAmount);
        Assert.Equal(TimeSpan.Zero, first.Value.CloseAtUtc.Offset);
        Assert.Equal(CheckedAt, first.Value.CheckedAtUtc);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task PostgreSqlReaderKeepsDraftAndInStockUnavailableAndExactCloseClosed()
    {
        await using var factory = await StartAndResetAsync();
        var published = await SeedPublishedAsync(factory, "status", withReservations: false);
        var draft = await SeedDraftPreOrderAsync(factory, "draft");
        var inStock = await SeedInStockAsync(factory, "instock");

        var draftResult = await QueryAsync(factory, draft, 1, CheckedAt);
        var inStockResult = await QueryAsync(factory, inStock, 1, CheckedAt);
        var exactClose = await QueryAsync(factory, published.ProductId, 1, published.CloseAtUtc);

        Assert.Equal(PreOrderCapacityErrors.NotAvailable, draftResult.Error);
        Assert.Equal(PreOrderCapacityErrors.NotAvailable, inStockResult.Error);
        Assert.Equal(PreOrderCapacityErrors.Closed, exactClose.Error);
    }

    [Fact]
    public async Task MissingOrIncoherentCapacityFailsAsSystemInvariant()
    {
        await using var factory = await StartAndResetAsync();
        var missing = await SeedPublishedProductWithoutCapacityAsync(factory, "missing-capacity");

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            QueryAsync(factory, missing, 1, CheckedAt));

        var seeded = await SeedPublishedAsync(factory, "bad-movement", withReservations: false);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.PreOrderCapacityMovements
                .Where(x => x.CapacityId == seeded.CapacityId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            QueryAsync(factory, seeded.ProductId, 1, CheckedAt));
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<Result<PreOrderCheckoutEligibilityResult>> QueryAsync(
        ToyStoreWebApplicationFactory factory,
        Guid productId,
        int quantity,
        DateTimeOffset now)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new GetPreOrderCheckoutEligibilityHandler(
            scope.ServiceProvider.GetRequiredService<IPreOrderCheckoutEligibilityReader>(),
            new FixedTimeProvider(now));
        var query = new GetPreOrderCheckoutEligibilityQuery(productId, quantity);
        return await new AuthorizationBehavior<GetPreOrderCheckoutEligibilityQuery,
            Result<PreOrderCheckoutEligibilityResult>>(new AllowedAuthorization()).Handle(
                query,
                token => handler.Handle(query, token),
                TestContext.Current.CancellationToken);
    }

    private static async Task<Seeded> SeedPublishedAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        bool withReservations)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (brand, product, offer) = ProductGraph(suffix, preOrder: true);
        product.Publish(product.Version, CreatedAt.AddMinutes(1), "publisher");
        var creation = PreOrderCapacity.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), offer!,
            "เปิดรอบ", $"product:{suffix}", CreatedAt.AddMinutes(1), "publisher");
        var movements = new List<PreOrderCapacityMovement> { creation.Movement };
        var reservations = new List<PreOrderCapacityReservation>();
        if (withReservations)
        {
            AddActiveExpired(creation.Capacity, reservations, movements);
            AddConsumed(creation.Capacity, reservations, movements);
            AddReleased(creation.Capacity, reservations, movements);
        }

        db.Brands.Add(brand);
        db.Products.Add(product);
        db.PreOrderCapacities.Add(creation.Capacity);
        db.PreOrderCapacityReservations.AddRange(reservations);
        db.PreOrderCapacityMovements.AddRange(movements);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seeded(product.Id, creation.Capacity.Id, offer!.CloseAtUtc);
    }

    private static void AddActiveExpired(
        PreOrderCapacity capacity,
        List<PreOrderCapacityReservation> reservations,
        List<PreOrderCapacityMovement> movements)
    {
        var at = CreatedAt.AddHours(1);
        var created = capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            at, at.AddMinutes(32), Guid.NewGuid(), "จอง", "active-expired",
            capacity.Version, "customer-1");
        reservations.Add(created.Reservation);
        movements.Add(created.Movement);
    }

    private static void AddConsumed(
        PreOrderCapacity capacity,
        List<PreOrderCapacityReservation> reservations,
        List<PreOrderCapacityMovement> movements)
    {
        var at = CreatedAt.AddHours(2);
        var created = capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            at, at.AddMinutes(32), Guid.NewGuid(), "จอง", "consumed",
            capacity.Version, "customer-1");
        var consumed = capacity.ConsumeReservation(
            created.Reservation, Guid.NewGuid(), "รับมัดจำ", "payment:1",
            capacity.Version, at.AddMinutes(5), "payment-system");
        reservations.Add(created.Reservation);
        movements.Add(created.Movement);
        movements.Add(consumed.Movement!);
    }

    private static void AddReleased(
        PreOrderCapacity capacity,
        List<PreOrderCapacityReservation> reservations,
        List<PreOrderCapacityMovement> movements)
    {
        var at = CreatedAt.AddHours(3);
        var created = capacity.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            at, at.AddMinutes(32), Guid.NewGuid(), "จอง", "released",
            capacity.Version, "customer-1");
        var released = capacity.ReleaseReservation(
            created.Reservation, Guid.NewGuid(), "ปล่อย", "released",
            capacity.Version, at.AddMinutes(5), "system");
        reservations.Add(created.Reservation);
        movements.Add(created.Movement);
        movements.Add(released.Movement!);
    }

    private static async Task<Guid> SeedDraftPreOrderAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (brand, product, _) = ProductGraph(suffix, preOrder: true);
        db.AddRange(brand, product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    private static async Task<Guid> SeedInStockAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (brand, product, _) = ProductGraph(suffix, preOrder: false);
        product.Publish(product.Version, CreatedAt.AddMinutes(1), "publisher");
        db.AddRange(brand, product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    private static async Task<Guid> SeedPublishedProductWithoutCapacityAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var (brand, product, _) = ProductGraph(suffix, preOrder: true);
        product.Publish(product.Version, CreatedAt.AddMinutes(1), "publisher");
        db.AddRange(brand, product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    private static (Brand Brand, Product Product, PreOrderOffer? Offer) ProductGraph(
        string suffix,
        bool preOrder)
    {
        var brand = Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), CreatedAt, "test");
        var image = new ProductImageDefinition(
            Guid.NewGuid(), $"products/{suffix}.webp", $"/media/products/{suffix}.webp", "รูปสินค้า");
        if (!preOrder)
        {
            return (brand, Product.CreateInStock(
                Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
                $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
                CatalogSeedIds.UnknownUniverse, InStockOffer.Create(Money.Create(1000)),
                [image], [], CreatedAt, "test"), null);
        }

        var offer = PreOrderOffer.Create(
            Money.Create(1000), Money.Create(200), new DateOnly(2026, 8, 1),
            EstimatedArrival.Create(8, 2026), 5, 3, CreatedAt, 7);
        return (brand, Product.CreatePreOrder(
            Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
            $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, offer, [image], [], CreatedAt, "test"), offer);
    }

    private static async Task<RowCounts> CountsAsync(ToyStoreWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return new RowCounts(
            await db.Products.CountAsync(TestContext.Current.CancellationToken),
            await db.PreOrderCapacities.CountAsync(TestContext.Current.CancellationToken),
            await db.PreOrderCapacityReservations.CountAsync(TestContext.Current.CancellationToken),
            await db.PreOrderCapacityMovements.CountAsync(TestContext.Current.CancellationToken));
    }

    private sealed record Seeded(Guid ProductId, Guid CapacityId, DateTimeOffset CloseAtUtc);
    private sealed record RowCounts(int Products, int Capacities, int Reservations, int Movements);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class AllowedAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policy,
            CancellationToken cancellationToken) => Task.FromResult(
                new CurrentUserAuthorizationResult(true, true, "customer-1"));
    }
}
