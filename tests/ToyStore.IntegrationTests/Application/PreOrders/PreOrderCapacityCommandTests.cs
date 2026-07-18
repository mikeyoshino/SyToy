using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.PreOrders;
using ToyStore.Application.PreOrders.ReservePreOrderCapacity;
using ToyStore.Application.PreOrders.TransitionPreOrderCapacity;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.PreOrders;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PreOrderCapacityCommandTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset Now =
        new(2026, 7, 17, 6, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task TwoCustomersCompetingForFinalCapacityProduceOneDurableWinner()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory, "race", total: 1, max: 1);
        var first = Reserve(seeded, Guid.NewGuid(), Guid.NewGuid(), 1, expectedVersion: 1);
        var second = Reserve(seeded, Guid.NewGuid(), Guid.NewGuid(), 1, expectedVersion: 1);

        var results = await Task.WhenAll(
            SendReserveAsync(factory, "customer-1", first),
            SendReserveAsync(factory, "customer-2", second));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Single(results, result => result.Error == PreOrderCapacityErrors.StaleVersion);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var capacity = await db.PreOrderCapacities.AsNoTracking().SingleAsync(
            item => item.Id == seeded.CapacityId,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, capacity.RemainingQuantity);
        Assert.Equal(1, capacity.HeldQuantity);
        Assert.Equal(1, await db.PreOrderCapacityReservations.CountAsync(
            item => item.CapacityId == seeded.CapacityId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CustomerAggregateLimitCountsActiveAndConsumedAndExactRetryIsIdempotent()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory, "limit", total: 3, max: 2);
        var operationId = Guid.NewGuid();
        var reservationId = Guid.NewGuid();
        var first = Reserve(seeded, reservationId, operationId, 2, expectedVersion: 1);

        var applied = await SendReserveAsync(factory, "customer-1", first);
        var retry = await SendReserveAsync(factory, "customer-1", first);
        var over = await SendReserveAsync(
            factory,
            "customer-1",
            Reserve(seeded, Guid.NewGuid(), Guid.NewGuid(), 1, expectedVersion: 2));

        Assert.True(applied.IsSuccess);
        Assert.True(applied.Value.Changed);
        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value.Changed);
        Assert.Equal(PreOrderCapacityErrors.CustomerLimitExceeded, over.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await db.PreOrderCapacityReservations.AsNoTracking().SingleAsync(
            item => item.Id == reservationId,
            TestContext.Current.CancellationToken);
        Assert.Equal(PreOrderCapacityPolicy.ReservationLifetime, reservation.ExpiresAtUtc - reservation.ReservedAtUtc);
    }

    [Fact]
    public async Task ReservationFromAnotherCapacityReturnsTypedNotFound()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedAsync(factory, "owner-first", total: 1, max: 1);
        var second = await SeedAsync(factory, "owner-second", total: 1, max: 1);
        var reservationId = Guid.NewGuid();
        var reserved = await SendReserveAsync(
            factory,
            "customer-1",
            Reserve(first, reservationId, Guid.NewGuid(), 1, expectedVersion: 1));
        var mismatched = new TransitionPreOrderCapacityCommand(
            second.CapacityId, second.ProductId, reservationId, Guid.NewGuid(),
            1, PreOrderCapacityAction.Release, "release", "other-capacity");

        var result = await SendTransitionAsync(factory, "payment-system", mismatched);

        Assert.True(reserved.IsSuccess);
        Assert.Equal(PreOrderCapacityErrors.ReservationNotFound, result.Error);
    }

    [Fact]
    public async Task ReusedReservationIdAcrossCapacitiesIsTypedConflictAndRollsBackMovement()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedAsync(factory, "duplicate-first", total: 1, max: 1);
        var second = await SeedAsync(factory, "duplicate-second", total: 1, max: 1);
        var reservationId = Guid.NewGuid();
        var firstResult = await SendReserveAsync(
            factory,
            "customer-1",
            Reserve(first, reservationId, Guid.NewGuid(), 1, expectedVersion: 1));
        var losingOperationId = Guid.NewGuid();

        var conflict = await SendReserveAsync(
            factory,
            "customer-1",
            Reserve(second, reservationId, losingOperationId, 1, expectedVersion: 1));

        Assert.True(firstResult.IsSuccess);
        Assert.Equal(PreOrderCapacityErrors.OperationConflict, conflict.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.PreOrderCapacityMovements.AnyAsync(
            item => item.Id == losingOperationId,
            TestContext.Current.CancellationToken));
        var secondCapacity = await db.PreOrderCapacities.AsNoTracking().SingleAsync(
            item => item.Id == second.CapacityId,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, secondCapacity.RemainingQuantity);
        Assert.Equal(0, secondCapacity.HeldQuantity);
        Assert.Equal(1, secondCapacity.Version);
    }

    [Fact]
    public async Task ConsumeAndReleaseCommandsPersistMovementsAndRejectChangedRetryEvidence()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedAsync(factory, "transition", total: 2, max: 2);
        var reservationId = Guid.NewGuid();
        var reserved = await SendReserveAsync(
            factory,
            "customer-1",
            Reserve(seeded, reservationId, Guid.NewGuid(), 1, expectedVersion: 1));
        var operationId = Guid.NewGuid();
        var consume = new TransitionPreOrderCapacityCommand(
            seeded.CapacityId, seeded.ProductId, reservationId, operationId,
            reserved.Value.Version, PreOrderCapacityAction.Consume, "รับมัดจำ", "payment:1");

        var applied = await SendTransitionAsync(factory, "payment-system", consume);
        var retry = await SendTransitionAsync(factory, "payment-system", consume);
        var conflict = await SendTransitionAsync(
            factory,
            "payment-system",
            consume with { Reference = "payment:other" });
        var changedReason = await SendTransitionAsync(
            factory,
            "payment-system",
            consume with { Reason = "เหตุผลอื่น" });
        var changedVersion = await SendTransitionAsync(
            factory,
            "payment-system",
            consume with { ExpectedVersion = consume.ExpectedVersion + 1 });
        var changedAction = await SendTransitionAsync(
            factory,
            "payment-system",
            consume with { Action = PreOrderCapacityAction.CancelAdminOrSupplier });

        Assert.True(applied.Value.Changed);
        Assert.False(retry.Value.Changed);
        Assert.Equal(PreOrderCapacityErrors.OperationConflict, conflict.Error);
        Assert.Equal(PreOrderCapacityErrors.OperationConflict, changedReason.Error);
        Assert.Equal(PreOrderCapacityErrors.OperationConflict, changedVersion.Error);
        Assert.Equal(PreOrderCapacityErrors.OperationConflict, changedAction.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(3, await db.PreOrderCapacityMovements.CountAsync(
            item => item.CapacityId == seeded.CapacityId,
            TestContext.Current.CancellationToken));
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        _ = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<Result<PreOrderCapacityMutationResult>> SendReserveAsync(
        ToyStoreWebApplicationFactory factory,
        string actor,
        ReservePreOrderCapacityCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ReservePreOrderCapacityHandler(
            scope.ServiceProvider.GetRequiredService<IPreOrderCapacityMutationSessionFactory>(),
            new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static async Task<Result<PreOrderCapacityMutationResult>> SendTransitionAsync(
        ToyStoreWebApplicationFactory factory,
        string actor,
        TransitionPreOrderCapacityCommand command)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new TransitionPreOrderCapacityHandler(
            scope.ServiceProvider.GetRequiredService<IPreOrderCapacityMutationSessionFactory>(),
            new FixedTimeProvider());
        return await Authorize(command, actor, token => handler.Handle(command, token));
    }

    private static Task<TResponse> Authorize<TRequest, TResponse>(
        TRequest request,
        string actor,
        Func<CancellationToken, Task<TResponse>> handler)
        where TRequest : notnull =>
        new AuthorizationBehavior<TRequest, TResponse>(new AllowedAuthorization(actor)).Handle(
            request,
            token => handler(token),
            TestContext.Current.CancellationToken);

    private static ReservePreOrderCapacityCommand Reserve(
        Seeded seeded,
        Guid reservationId,
        Guid operationId,
        int quantity,
        long expectedVersion) =>
        new(
            seeded.CapacityId,
            seeded.ProductId,
            reservationId,
            Guid.NewGuid(),
            operationId,
            expectedVersion,
            quantity,
            "เริ่มชำระมัดจำ",
            $"checkout:{reservationId:N}");

    private static async Task<Seeded> SeedAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        int total,
        int max)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), Now.AddMinutes(-2), "test");
        var offer = PreOrderOffer.Create(
            Money.Create(1000), Money.Create(200), new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027), total, max, Now.AddMinutes(-1));
        var product = Product.CreatePreOrder(
            Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
            $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, offer, Now.AddMinutes(-1), "test");
        var creation = PreOrderCapacity.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), offer,
            "เปิด capacity", $"product:{suffix}", Now.AddMinutes(-1), "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.PreOrderCapacities.Add(creation.Capacity);
        db.PreOrderCapacityMovements.Add(creation.Movement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new Seeded(creation.Capacity.Id, product.Id);
    }

    private sealed record Seeded(Guid CapacityId, Guid ProductId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private sealed class AllowedAuthorization(string actor) : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, actor));
    }
}
