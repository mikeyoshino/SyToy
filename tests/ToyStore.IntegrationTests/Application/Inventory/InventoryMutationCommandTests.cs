using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Application.Inventory.AdjustStock;
using ToyStore.Application.Inventory.ReceiveStock;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Application.Inventory;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InventoryMutationCommandTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ReceiveHappyExactAndEveryChangedIntentFieldAreDurableAndTyped()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "receive-handler", 2);
        var operationId = Guid.NewGuid();
        var command = Receive(seeded, operationId, 1, 1);

        var applied = await ExecuteReceiveAsync(factory, command);
        var exact = await ExecuteReceiveAsync(factory, command);
        var conflicts = new[]
        {
            await ExecuteReceiveAsync(factory, command with { Quantity = 2 }),
            await ExecuteReceiveAsync(factory, command with { Reason = "เหตุผลอื่น" }),
            await ExecuteReceiveAsync(factory, command with { Reference = "other" }),
            await ExecuteReceiveAsync(factory, command with { ExpectedVersion = 2 }),
            await ExecuteReceiveAsync(factory, command, "other-admin"),
            await ExecuteAdjustAsync(factory, new AdjustStockCommand(
                command.InventoryItemId, command.ProductId, command.OperationId,
                command.ExpectedVersion, 1, command.Reason, command.Reference)),
        };

        Assert.True(applied.IsSuccess);
        Assert.True(applied.Value.Changed);
        Assert.False(exact.Value.Changed);
        Assert.All(conflicts, result =>
            Assert.Equal(InventoryErrors.OperationConflict, result.Error));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            current => current.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, item.OnHandQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(item.UpdatedAtUtc, applied.Value.UpdatedAtUtc);
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == operationId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NotFoundStaleLowerHeldOverflowAndVersionExhaustionRollBackCleanly()
    {
        await using var factory = await StartAndResetAsync();
        var absent = await ExecuteReceiveAsync(factory, new ReceiveStockCommand(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1,
            "รับสินค้า", "missing"));
        Assert.Equal(InventoryErrors.NotFound, absent.Error);

        var normal = await SeedInventoryAsync(factory, "error-handler", 2);
        var stale = await ExecuteReceiveAsync(
            factory,
            Receive(normal, Guid.NewGuid(), 2, 1));
        Assert.Equal(InventoryErrors.StaleVersion, stale.Error);

        var held = await SeedInventoryAsync(factory, "held-handler", 2);
        await SeedReservationAsync(factory, held, 2);
        var insufficientOperation = Guid.NewGuid();
        var insufficient = await ExecuteAdjustAsync(factory, new AdjustStockCommand(
            held.InventoryId, held.ProductId, insufficientOperation, 2, -1,
            "ปรับลด", "held"));
        Assert.Equal(InventoryErrors.InsufficientOnHand, insufficient.Error);

        var maximum = await SeedInventoryAsync(factory, "overflow-handler", int.MaxValue);
        var overflowOperation = Guid.NewGuid();
        var overflow = await ExecuteReceiveAsync(
            factory,
            Receive(maximum, overflowOperation, 1, 1));
        Assert.Equal(InventoryErrors.QuantityOverflow, overflow.Error);

        await using (var mutationScope = factory.Services.CreateAsyncScope())
        {
            var db = mutationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InventoryItems"
                SET "Version" = {long.MaxValue}
                WHERE "Id" = {normal.InventoryId}
                """,
                TestContext.Current.CancellationToken);
        }

        var exhaustedOperation = Guid.NewGuid();
        var exhausted = await ExecuteReceiveAsync(
            factory,
            Receive(normal, exhaustedOperation, long.MaxValue, 1));
        Assert.Equal(InventoryErrors.VersionExhausted, exhausted.Error);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>();
        Assert.False(await verification.StockMovements.AnyAsync(
            movement => movement.Id == insufficientOperation
                || movement.Id == overflowOperation
                || movement.Id == exhaustedOperation,
            TestContext.Current.CancellationToken));
        var heldItem = await verification.InventoryItems.AsNoTracking().SingleAsync(
            item => item.Id == held.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, heldItem.OnHandQuantity);
        Assert.Equal(2, heldItem.HeldQuantity);
    }

    [Fact]
    public async Task AdjustHappyExactAndChangedIntentAreDurableAndTyped()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "adjust-handler", 2);
        var command = new AdjustStockCommand(
            seeded.InventoryId, seeded.ProductId, Guid.NewGuid(), 1, 1,
            "ปรับสต็อก", "adjust-handler");

        var applied = await ExecuteAdjustAsync(factory, command);
        var exact = await ExecuteAdjustAsync(factory, command);
        var changed = await ExecuteAdjustAsync(
            factory,
            command with { Reason = "เหตุผลอื่น" });

        Assert.True(applied.Value.Changed);
        Assert.False(exact.Value.Changed);
        Assert.Equal(InventoryErrors.OperationConflict, changed.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            current => current.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, item.OnHandQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == command.OperationId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentReceiveAdjustFromSameVersionHaveOneWinnerAndOneStale()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "handler-race", 2);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<InventoryMutationResult>> RunAsync(bool receive)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return receive
                ? await ExecuteReceiveAsync(
                    factory,
                    Receive(seeded, Guid.NewGuid(), 1, 1))
                : await ExecuteAdjustAsync(factory, new AdjustStockCommand(
                    seeded.InventoryId, seeded.ProductId, Guid.NewGuid(), 1, -1,
                    "ปรับลด", "race"));
        }

        var results = await Task.WhenAll(RunAsync(true), RunAsync(false));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            InventoryErrors.StaleVersion,
            Assert.Single(results, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            current => current.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.True(item.OnHandQuantity is 1 or 3);
        Assert.Equal(2, item.Version);
        Assert.Equal(2, await db.StockMovements.CountAsync(
            movement => movement.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CrossInventoryDuplicateOperationUsesFreshEvidenceAndLeavesLoserUnchanged()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedInventoryAsync(factory, "handler-pk-first", 2);
        var second = await SeedInventoryAsync(factory, "handler-pk-second", 2);
        var operationId = Guid.NewGuid();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<InventoryMutationResult>> RunAsync(SeededInventory seeded)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await ExecuteReceiveAsync(
                factory,
                Receive(seeded, operationId, 1, 1));
        }

        var results = await Task.WhenAll(RunAsync(first), RunAsync(second));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            InventoryErrors.OperationConflict,
            Assert.Single(results, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await db.InventoryItems.AsNoTracking()
            .Where(item => item.Id == first.InventoryId || item.Id == second.InventoryId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(items, item => item.OnHandQuantity == 3 && item.Version == 2);
        Assert.Single(items, item => item.OnHandQuantity == 2 && item.Version == 1);
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == operationId,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExactRetryRejectsCorruptedEqualVersionOwningRow(bool receive)
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(
            factory,
            receive ? "corrupt-receive" : "corrupt-adjust",
            2);
        var operationId = Guid.NewGuid();
        if (receive)
        {
            Assert.True((await ExecuteReceiveAsync(
                factory,
                Receive(seeded, operationId, 1, 1))).IsSuccess);
        }
        else
        {
            Assert.True((await ExecuteAdjustAsync(factory, new AdjustStockCommand(
                seeded.InventoryId, seeded.ProductId, operationId, 1, 1,
                "ปรับสต็อก", "corrupt-adjust"))).IsSuccess);
        }

        await using (var corruptionScope = factory.Services.CreateAsyncScope())
        {
            var db = corruptionScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InventoryItems"
                SET "UpdatedBy" = 'corrupted-actor'
                WHERE "Id" = {seeded.InventoryId}
                """,
                TestContext.Current.CancellationToken);
        }

        if (receive)
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ExecuteReceiveAsync(factory, Receive(seeded, operationId, 1, 1)));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                ExecuteAdjustAsync(factory, new AdjustStockCommand(
                    seeded.InventoryId, seeded.ProductId, operationId, 1, 1,
                    "ปรับสต็อก", "corrupt-adjust")));
        }
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<Result<InventoryMutationResult>> ExecuteReceiveAsync(
        ToyStoreWebApplicationFactory factory,
        ReceiveStockCommand command,
        string actor = "admin-1")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new ReceiveStockHandler(
            scope.ServiceProvider.GetRequiredService<IInventoryMutationSessionFactory>(),
            new InventoryCommitOutcomeResolver(
                NullLogger<InventoryCommitOutcomeResolver>.Instance),
            scope.ServiceProvider.GetRequiredService<IPersistenceFailureClassifier>(),
            new FixedTimeProvider());
        return await new AuthorizationBehavior<
            ReceiveStockCommand,
            Result<InventoryMutationResult>>(new AdminAuthorization(actor)).Handle(
                command,
                token => handler.Handle(command, token),
                TestContext.Current.CancellationToken);
    }

    private static async Task<Result<InventoryMutationResult>> ExecuteAdjustAsync(
        ToyStoreWebApplicationFactory factory,
        AdjustStockCommand command,
        string actor = "admin-1")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var handler = new AdjustStockHandler(
            scope.ServiceProvider.GetRequiredService<IInventoryMutationSessionFactory>(),
            new InventoryCommitOutcomeResolver(
                NullLogger<InventoryCommitOutcomeResolver>.Instance),
            scope.ServiceProvider.GetRequiredService<IPersistenceFailureClassifier>(),
            new FixedTimeProvider());
        return await new AuthorizationBehavior<
            AdjustStockCommand,
            Result<InventoryMutationResult>>(new AdminAuthorization(actor)).Handle(
                command,
                token => handler.Handle(command, token),
                TestContext.Current.CancellationToken);
    }

    private static ReceiveStockCommand Receive(
        SeededInventory seeded,
        Guid operationId,
        long version,
        int quantity) => new(
            seeded.InventoryId, seeded.ProductId, operationId, version, quantity,
            "รับสินค้า", "receive-handler");

    private static async Task<SeededInventory> SeedInventoryAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        int stock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), UtcNow, "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
            $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, InStockOffer.Create(Money.Create(100)),
            UtcNow, "test");
        var creation = InventoryItem.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), stock,
            "สินค้าเริ่มต้น", $"initial-{suffix}", UtcNow, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.InventoryItems.Add(creation.Item);
        db.StockMovements.Add(creation.InitialMovement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededInventory(creation.Item.Id, product.Id);
    }

    private static async Task SeedReservationAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        int quantity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.SingleAsync(
            current => current.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var reservation = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), quantity, UtcNow.AddMinutes(1),
            UtcNow.AddMinutes(16), "รอชำระ", "held-handler", 1, "system");
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private sealed record SeededInventory(Guid InventoryId, Guid ProductId);

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow.AddMinutes(2).AddTicks(7);
    }

    private sealed class AdminAuthorization(string actor) : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, actor));
    }
}
