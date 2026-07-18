using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InventoryMutationSessionTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task MissingInventoryIsMaterializedOnceAndConcurrentInsertIsNeverLoadedUnlocked()
    {
        await using var factory = await StartAndResetAsync();
        var productId = await SeedProductAsync(factory, "missing-lock");
        var inventoryId = Guid.NewGuid();
        var inserted = 0;
        async Task InsertAsync()
        {
            if (Interlocked.Exchange(ref inserted, 1) != 0)
            {
                return;
            }

            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var creation = InventoryItem.Create(
                inventoryId, productId, Guid.NewGuid(), 0,
                "สินค้าเริ่มต้น", "concurrent-insert", UtcNow, "test");
            db.InventoryItems.Add(creation.Item);
            db.StockMovements.Add(creation.InitialMovement);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var interceptor = new SplitLockInsertInterceptor(InsertAsync);
        await using var provider = CreateProvider(interceptor);
        var sessionFactory = provider.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);

        var execution = await session.ExecuteOnceAsync(async cancellationToken =>
        {
            var item = await session.LockInventoryAsync(
                inventoryId, productId, cancellationToken);
            await InsertAsync();
            return Result<bool>.Success(item is not null);
        }, TestContext.Current.CancellationToken);

        Assert.False(execution.Result.Value);
        Assert.Equal(1, inserted);
    }

    [Fact]
    public async Task CreationAndTypedFailureAreAtomicAndSessionExecutesOnlyOnce()
    {
        await using var factory = await StartAndResetAsync();
        var productId = await SeedProductAsync(factory, "creation");
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var creation = InventoryItem.Create(
            Guid.NewGuid(), productId, Guid.NewGuid(), 0,
            "สินค้าเริ่มต้น", "create-session", UtcNow, "admin");
        var callbacks = 0;

        var execution = await session.ExecuteOnceAsync(
            _ =>
            {
                callbacks++;
                session.Add(creation);
                return Task.FromResult(Result<Guid>.Success(creation.Item.Id));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(InventoryCommitOutcome.Committed, execution.CommitOutcome);
        Assert.Equal(1, callbacks);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteOnceAsync(
            _ => Task.FromResult(Result<Guid>.Success(Guid.NewGuid())),
            TestContext.Current.CancellationToken));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.True(await db.InventoryItems.AnyAsync(
                item => item.Id == creation.Item.Id,
                TestContext.Current.CancellationToken));
            Assert.True(await db.StockMovements.AnyAsync(
                movement => movement.Id == creation.InitialMovement.Id,
                TestContext.Current.CancellationToken));
        }

        var rolledBackProductId = await SeedProductAsync(factory, "creation-rollback");
        var rolledBack = InventoryItem.Create(
            Guid.NewGuid(), rolledBackProductId, Guid.NewGuid(), 1,
            "สินค้าเริ่มต้น", "rollback-session", UtcNow, "admin");
        await using var rollbackSession = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        var rollbackExecution = await rollbackSession.ExecuteOnceAsync<Guid>(
            _ =>
            {
                rollbackSession.Add(rolledBack);
                return Task.FromResult(Result<Guid>.Failure(TestFailure));
            },
            TestContext.Current.CancellationToken);
        Assert.Equal(
            InventoryCommitOutcome.DefinitelyRolledBack,
            rollbackExecution.CommitOutcome);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verification.InventoryItems.AnyAsync(
            item => item.Id == rolledBack.Item.Id,
            TestContext.Current.CancellationToken));
        Assert.False(await verification.StockMovements.AnyAsync(
            movement => movement.Id == rolledBack.InitialMovement.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ExactOperationRetryPrecedesStaleVersionAndCrossInventoryReuseConflicts()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedInventoryAsync(factory, "retry-a", 2);
        var second = await SeedInventoryAsync(factory, "retry-b", 2);
        var operationId = Guid.NewGuid();

        var applied = await ReceiveAsync(factory, first, operationId, expectedVersion: 1, quantity: 2);
        var exactRetry = await ReceiveAsync(factory, first, operationId, expectedVersion: 1, quantity: 2);
        var changedReason = await ReceiveAsync(
            factory, first, operationId, 1, 2, reason: "เหตุผลอื่น");
        var changedReference = await ReceiveAsync(
            factory, first, operationId, 1, 2, reference: "other-reference");
        var changedActor = await ReceiveAsync(
            factory, first, operationId, 1, 2, actor: "other-admin");
        var changedSourceVersion = await ReceiveAsync(
            factory, first, operationId, 2, 2);
        var conflict = await ReceiveAsync(factory, second, operationId, expectedVersion: 1, quantity: 2);

        Assert.Equal(InventoryCommitOutcome.Committed, applied.CommitOutcome);
        Assert.True(applied.Result.IsSuccess);
        Assert.Equal(InventoryCommitOutcome.Committed, exactRetry.CommitOutcome);
        Assert.True(exactRetry.Result.IsSuccess);
        Assert.Equal("unchanged", exactRetry.Result.Value);
        Assert.Equal(OperationConflict, changedReason.Result.Error);
        Assert.Equal(OperationConflict, changedReference.Result.Error);
        Assert.Equal(OperationConflict, changedActor.Result.Error);
        Assert.Equal(OperationConflict, changedSourceVersion.Result.Error);
        Assert.Equal(InventoryCommitOutcome.DefinitelyRolledBack, conflict.CommitOutcome);
        Assert.Equal(OperationConflict, conflict.Result.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.InventoryItems
            .Where(item => item.Id == first.InventoryId)
            .Select(item => item.OnHandQuantity)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await db.InventoryItems
            .Where(item => item.Id == second.InventoryId)
            .Select(item => item.OnHandQuantity)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == operationId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentMatchingAndConflictingRetriesHaveOneDurableOperationEffect()
    {
        await using var factory = await StartAndResetAsync();
        var matching = await SeedInventoryAsync(factory, "retry-matching", 4);
        var matchingOperationId = Guid.NewGuid();

        var matchingResults = await Task.WhenAll(
            ReceiveAsync(factory, matching, matchingOperationId, 1, 1),
            ReceiveAsync(factory, matching, matchingOperationId, 1, 1));

        Assert.All(matchingResults, result => Assert.True(result.Result.IsSuccess));
        Assert.Equal(1, matchingResults.Count(result => result.Result.Value == "changed"));
        Assert.Equal(1, matchingResults.Count(result => result.Result.Value == "unchanged"));

        var conflicting = await SeedInventoryAsync(factory, "retry-conflicting", 4);
        var conflictingOperationId = Guid.NewGuid();
        var conflictingResults = await Task.WhenAll(
            ReceiveAsync(factory, conflicting, conflictingOperationId, 1, 1),
            ReceiveAsync(factory, conflicting, conflictingOperationId, 1, 2));

        Assert.Equal(1, conflictingResults.Count(result => result.Result.IsSuccess));
        Assert.Equal(1, conflictingResults.Count(
            result => result.Result.IsFailure && result.Result.Error == OperationConflict));

        var first = await SeedInventoryAsync(factory, "retry-global-first", 2);
        var second = await SeedInventoryAsync(factory, "retry-global-second", 2);
        var globalOperationId = Guid.NewGuid();
        var globalResults = await Task.WhenAll(
            ReceiveAsync(factory, first, globalOperationId, 1, 1),
            ReceiveAsync(factory, second, globalOperationId, 1, 1));

        Assert.Equal(1, globalResults.Count(result => result.Result.IsSuccess));
        Assert.Equal(1, globalResults.Count(
            result => result.Result.IsFailure && result.Result.Error == OperationConflict));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == matchingOperationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == conflictingOperationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.Id == globalOperationId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements
            .Where(movement => movement.Id == globalOperationId)
            .SumAsync(
                movement => movement.QuantityDelta,
                TestContext.Current.CancellationToken));
        var globalItems = await db.InventoryItems.AsNoTracking()
            .Where(item => item.Id == first.InventoryId || item.Id == second.InventoryId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(globalItems, item => item.OnHandQuantity == 3 && item.Version == 2);
        Assert.Single(globalItems, item => item.OnHandQuantity == 2 && item.Version == 1);
    }

    [Fact]
    public async Task SameVersionReceiveRaceSerializesToOneMutationAndOneStaleFailure()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "receive-race", 2);
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<InventoryMutationExecution<string>> RunAsync(Guid operationId)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await ReceiveAsync(factory, seeded, operationId, 1, 1);
        }

        var results = await Task.WhenAll(RunAsync(Guid.NewGuid()), RunAsync(Guid.NewGuid()));

        Assert.Single(results, result => result.Result.IsSuccess);
        Assert.Equal(Stale, Assert.Single(results, result => result.Result.IsFailure).Result.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(3, item.OnHandQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(2, await db.StockMovements.CountAsync(
            movement => movement.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LastUnitReserveRaceCreatesOneFailClosedHold()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "reserve-race", 1);
        var results = await Task.WhenAll(
            ReserveAsync(factory, seeded, Guid.NewGuid(), expectedVersion: 1, quantity: 1),
            ReserveAsync(factory, seeded, Guid.NewGuid(), expectedVersion: 1, quantity: 1));

        Assert.Single(results, result => result.Result.IsSuccess);
        Assert.Single(results, result => result.Result.IsFailure);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(1, item.OnHandQuantity);
        Assert.Equal(1, item.HeldQuantity);
        Assert.Equal(0, item.ReservableQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(1, await db.StockReservations.CountAsync(
            reservation => reservation.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReleaseConsumeRaceHasOneTerminalWinnerAndConditionalMovement()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "terminal-race", 2);
        var reserved = await ReserveAsync(
            factory, seeded, Guid.NewGuid(), expectedVersion: 1, quantity: 1);
        var reservationId = reserved.Result.Value;

        var results = await Task.WhenAll(
            TransitionAsync(factory, seeded, reservationId, consume: false),
            TransitionAsync(factory, seeded, reservationId, consume: true));

        Assert.Single(results, result => result.Result.IsSuccess);
        Assert.Single(results, result => result.Result.IsFailure);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reservation = await db.StockReservations.AsNoTracking().SingleAsync(
            value => value.Id == reservationId,
            TestContext.Current.CancellationToken);
        var consumeMovements = await db.StockMovements.CountAsync(
            movement => movement.ReservationId == reservationId,
            TestContext.Current.CancellationToken);
        Assert.Contains(
            reservation.Status,
            new[] { StockReservationStatus.Released, StockReservationStatus.Consumed });
        Assert.Equal(
            reservation.Status == StockReservationStatus.Consumed ? 1 : 0,
            consumeMovements);
    }

    [Fact]
    public async Task ReserveAndDownwardAdjustBothLinearizationsPreserveBounds()
    {
        await using var factory = await StartAndResetAsync();
        var reserveFirst = await SeedInventoryAsync(factory, "reserve-first", 2);
        var reserve = await ReserveAsync(
            factory, reserveFirst, Guid.NewGuid(), expectedVersion: 1, quantity: 2);
        var adjustAfterReserve = await AdjustAsync(
            factory, reserveFirst, Guid.NewGuid(), expectedVersion: 2, delta: -1);
        Assert.True(reserve.Result.IsSuccess);
        Assert.Equal(Insufficient, adjustAfterReserve.Result.Error);

        var adjustFirst = await SeedInventoryAsync(factory, "adjust-first", 2);
        var adjust = await AdjustAsync(
            factory, adjustFirst, Guid.NewGuid(), expectedVersion: 1, delta: -1);
        var reserveAfterAdjust = await ReserveAsync(
            factory, adjustFirst, Guid.NewGuid(), expectedVersion: 2, quantity: 2);
        Assert.True(adjust.Result.IsSuccess);
        Assert.Equal(Insufficient, reserveAfterAdjust.Result.Error);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var items = await db.InventoryItems.AsNoTracking()
            .Where(item => item.Id == reserveFirst.InventoryId || item.Id == adjustFirst.InventoryId)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.All(items, item =>
        {
            Assert.True(item.OnHandQuantity >= 0);
            Assert.True(item.HeldQuantity >= 0);
            Assert.True(item.HeldQuantity <= item.OnHandQuantity);
        });
    }

    [Fact]
    public async Task OverlappingSameVersionReserveAndAdjustSerializeWithoutTimeout()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "reserve-adjust-overlap", 2);
        var interceptor = new ForUpdateAttemptInterceptor();
        await using var provider = CreateProvider(interceptor);
        var sessionFactory = provider.GetRequiredService<IInventoryMutationSessionFactory>();
        var firstLocked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<InventoryMutationExecution<string>> ReserveFirstAsync()
        {
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            return await session.ExecuteOnceAsync<string>(async cancellationToken =>
            {
                var item = await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken);
                Assert.NotNull(item);
                firstLocked.TrySetResult();
                await releaseFirst.Task.WaitAsync(cancellationToken);
                var reservation = item.Reserve(
                    Guid.NewGuid(), Guid.NewGuid(), 1, UtcNow.AddMinutes(1),
                    UtcNow.AddHours(1), "รอชำระ", "overlap-reserve",
                    1, "system");
                session.Add(reservation);
                return Result<string>.Success("reserved");
            }, TestContext.Current.CancellationToken);
        }

        async Task<InventoryMutationExecution<string>> AdjustSecondAsync()
        {
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            return await session.ExecuteOnceAsync<string>(async cancellationToken =>
            {
                var item = await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken);
                Assert.NotNull(item);
                if (item.Version != 1)
                {
                    return Result<string>.Failure(Stale);
                }

                var movement = item.AdjustStock(
                    Guid.NewGuid(), -1, "ปรับลด", "overlap-adjust", 1,
                    UtcNow.AddMinutes(1), "admin");
                session.Add(movement);
                return Result<string>.Success("adjusted");
            }, TestContext.Current.CancellationToken);
        }

        var reserveTask = ReserveFirstAsync();
        Task<InventoryMutationExecution<string>>? adjustTask = null;
        InventoryMutationExecution<string>[] results;
        try
        {
            await firstLocked.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            adjustTask = AdjustSecondAsync();
            await interceptor.SecondAttempted.Task.WaitAsync(
                TimeSpan.FromSeconds(5),
                TestContext.Current.CancellationToken);
            releaseFirst.TrySetResult();
            results = await Task.WhenAll(reserveTask, adjustTask)
                .WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
        }
        finally
        {
            releaseFirst.TrySetResult();
            if (adjustTask is not null)
            {
                await Task.WhenAll(reserveTask, adjustTask)
                    .WaitAsync(
                        TimeSpan.FromSeconds(5),
                        TestContext.Current.CancellationToken);
            }
            else
            {
                await reserveTask.WaitAsync(
                    TimeSpan.FromSeconds(5),
                    TestContext.Current.CancellationToken);
            }
        }

        Assert.Single(results, result => result.Result.IsSuccess);
        Assert.Equal(Stale, Assert.Single(results, result => result.Result.IsFailure).Result.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            current => current.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, item.OnHandQuantity);
        Assert.Equal(1, item.HeldQuantity);
        Assert.Equal(2, item.Version);
        Assert.Equal(1, await db.StockReservations.CountAsync(
            reservation => reservation.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DirectStaleEfSnapshotRollsBackLosingMovement()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "ef-stale", 2);
        await using var staleScope = factory.Services.CreateAsyncScope();
        var staleDb = staleScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var staleItem = await staleDb.InventoryItems.SingleAsync(
            item => item.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        await using (var competingScope = factory.Services.CreateAsyncScope())
        {
            var competingDb = competingScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            await competingDb.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InventoryItems"
                SET "OnHandQuantity" = 3,
                    "Version" = 2,
                    "UpdatedAtUtc" = {UtcNow.AddMinutes(1)},
                    "UpdatedBy" = {"competing-admin"}
                WHERE "Id" = {seeded.InventoryId}
                """,
                TestContext.Current.CancellationToken);
        }

        var losingMovement = staleItem.ReceiveStock(
            Guid.NewGuid(), 1, "รับเข้า", "stale-loser", 1,
            UtcNow.AddMinutes(1), "admin");
        staleDb.StockMovements.Add(losingMovement);

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => staleDb.SaveChangesAsync(TestContext.Current.CancellationToken));

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verification.StockMovements.AnyAsync(
            movement => movement.Id == losingMovement.Id,
            TestContext.Current.CancellationToken));
        var authoritative = await verification.InventoryItems
            .AsNoTracking()
            .SingleAsync(
                item => item.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
        Assert.Equal(3, authoritative.OnHandQuantity);
        Assert.Equal(2, authoritative.Version);
    }

    [Fact]
    public async Task CommitVerificationDistinguishesCommittedSupersededInconsistentAndUnavailable()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "verification", 2);
        var applied = await ReceiveWithEvidenceAsync(
            factory, seeded, Guid.NewGuid(), expectedVersion: 1, quantity: 1);
        var evidence = Assert.IsType<InventoryMutationEvidence>(applied.Result.Value.Evidence);
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();

        var committed = await sessionFactory.VerifyCommitAsync(
            evidence,
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Committed, committed.Outcome);

        var conflictingCreation = InventoryItem.Create(
            Guid.NewGuid(), seeded.ProductId, Guid.NewGuid(), 2,
            "สินค้าเริ่มต้น", "conflicting-intent", UtcNow, "admin");
        var conflictingMovement = conflictingCreation.Item.ReceiveStock(
            evidence.OperationId, 1, "รับสินค้า", "receive-session", 1,
            UtcNow.AddMinutes(1).AddTicks(7), "admin");
        var conflicting = await sessionFactory.VerifyCommitAsync(
            InventoryMutationEvidence.Capture(
                conflictingCreation.Item,
                conflictingMovement),
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Conflict, conflicting.Outcome);

        await using (var corruptionScope = factory.Services.CreateAsyncScope())
        {
            var db = corruptionScope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "InventoryItems"
                SET "UpdatedBy" = {"corrupted"}
                WHERE "Id" = {seeded.InventoryId}
                """,
                TestContext.Current.CancellationToken);
        }

        var inconsistent = await sessionFactory.VerifyCommitAsync(
            evidence,
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Inconsistent, inconsistent.Outcome);

        await ReceiveAsync(factory, seeded, Guid.NewGuid(), expectedVersion: 2, quantity: 1);
        var superseded = await sessionFactory.VerifyCommitAsync(evidence, CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Superseded, superseded.Outcome);
        Assert.Equal(3, superseded.AuthoritativeEvidence.IntendedVersion);

        var absentCreation = InventoryItem.Create(
            Guid.NewGuid(), seeded.ProductId, Guid.NewGuid(), 0,
            "สินค้าเริ่มต้น", "absent", UtcNow, "admin");
        var absent = await sessionFactory.VerifyCommitAsync(
            InventoryMutationEvidence.Capture(
                absentCreation.Item,
                absentCreation.InitialMovement),
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Inconsistent, absent.Outcome);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=127.0.0.1;Port=1;Database=unavailable_test;Username=test;Password=test;Timeout=1;Command Timeout=1;Pooling=false",
            })
            .Build());
        await using var provider = services.BuildServiceProvider();
        var unavailable = await provider
            .GetRequiredService<IInventoryMutationSessionFactory>()
            .VerifyCommitAsync(evidence, CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Unavailable, unavailable.Outcome);
    }

    [Fact]
    public async Task CommitAcknowledgementFailureIsIndeterminateAndCallbackIsNeverReplayed()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "commit-unknown", 2);
        var interceptor = new CommitAcknowledgementFailureInterceptor();
        await using var provider = CreateProvider(interceptor);
        var sessionFactory = provider.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var callbacks = 0;

        var execution = await session.ExecuteOnceAsync(
            async cancellationToken =>
            {
                callbacks++;
                var item = await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken);
                Assert.NotNull(item);
                var movement = item.ReceiveStock(
                    Guid.NewGuid(), 1, "รับเข้า", "commit-unknown", item.Version,
                    UtcNow.AddMinutes(1), "admin");
                session.Add(movement);
                return Result<InventoryMutationEvidence>.Success(
                    InventoryMutationEvidence.Capture(item, movement));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, callbacks);
        Assert.Equal(1, interceptor.InvocationCount);
        Assert.Equal(InventoryCommitOutcome.Indeterminate, execution.CommitOutcome);
        Assert.NotNull(execution.CommitFailure);
        var verified = await sessionFactory.VerifyCommitAsync(
            execution.Result.Value,
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Committed, verified.Outcome);
    }

    [Fact]
    public async Task CommitCancellationReconcilesDurableEvidenceThenRethrowsWithoutReplayingCallback()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "commit-cancel", 2);
        var cancellation = new OperationCanceledException("commit acknowledgement cancelled");
        var interceptor = new CommitAcknowledgementCancellationInterceptor(cancellation);
        await using var provider = CreateProvider(interceptor);
        var sessionFactory = provider.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        var callbacks = 0;

        var execution = await session.ExecuteOnceAsync(
            async cancellationToken =>
            {
                callbacks++;
                var item = await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken);
                Assert.NotNull(item);
                var movement = item.ReceiveStock(
                    Guid.NewGuid(), 1, "รับเข้า", "commit-cancel", item.Version,
                    UtcNow.AddMinutes(1), "admin");
                session.Add(movement);
                return Result<InventoryMutationEvidence>.Success(
                    InventoryMutationEvidence.Capture(item, movement));
            },
            TestContext.Current.CancellationToken);

        var resolver = new InventoryCommitOutcomeResolver(
            NullLogger<InventoryCommitOutcomeResolver>.Instance);
        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(
                execution,
                token => sessionFactory.VerifyCommitAsync(execution.Result.Value, token),
                static evidence => evidence,
                "ReceiveStock",
                new CancellationToken(canceled: true)));

        Assert.Same(cancellation, thrown);
        Assert.Equal(1, callbacks);
        Assert.Equal(1, interceptor.InvocationCount);
        var verified = await sessionFactory.VerifyCommitAsync(
            execution.Result.Value,
            CancellationToken.None);
        Assert.Equal(InventoryCommitVerification.Committed, verified.Outcome);
    }

    [Fact]
    public async Task PreCommitCancellationRollsBackAndReciprocalEvidenceMismatchFailsClosed()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "cancel", 2);
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        var operationId = Guid.NewGuid();
        await using (var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken))
        {
            await Assert.ThrowsAsync<OperationCanceledException>(() => session.ExecuteOnceAsync<string>(
                async cancellationToken =>
                {
                    var item = await session.LockInventoryAsync(
                        seeded.InventoryId, seeded.ProductId, cancellationToken);
                    Assert.NotNull(item);
                    var movement = item.ReceiveStock(
                        operationId, 1, "รับเข้า", "cancel", item.Version,
                        UtcNow.AddMinutes(1), "admin");
                    session.Add(movement);
                    throw new OperationCanceledException(cancellationToken);
                },
                TestContext.Current.CancellationToken));
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(await db.StockMovements.AnyAsync(
                movement => movement.Id == operationId,
                TestContext.Current.CancellationToken));
            Assert.Equal(2, await db.InventoryItems
                .Where(item => item.Id == seeded.InventoryId)
                .Select(item => item.OnHandQuantity)
                .SingleAsync(TestContext.Current.CancellationToken));
        }

        var crossWired = await SeedCrossWiredConsumedReservationAsync(factory, seeded);
        await using var validationSession = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            validationSession.ExecuteOnceAsync<string>(async cancellationToken =>
            {
                Assert.NotNull(await validationSession.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken));
                await validationSession.FindReservationAsync(crossWired, cancellationToken);
                return Result<string>.Success("invalid");
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NonConsumedReservationWithLinkedConsumeMovementFailsClosed()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "active-consume-evidence", 2);
        var reservationId = await SeedActiveReservationWithConsumeMovementAsync(
            factory,
            seeded);
        var sessionFactory = factory.Services
            .GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.ExecuteOnceAsync<string>(async cancellationToken =>
            {
                Assert.NotNull(await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken));
                await session.FindReservationAsync(reservationId, cancellationToken);
                return Result<string>.Success("invalid");
            }, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("quantity")]
    [InlineData("terminal-time")]
    [InlineData("actor")]
    [InlineData("reason")]
    [InlineData("reference")]
    [InlineData("version-ahead")]
    public async Task ConsumedReservationRequiresExactReciprocalEvidence(string corruption)
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, $"consume-{corruption}", 2);
        var consumed = await SeedConsumedReservationAsync(factory, seeded);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            switch (corruption)
            {
                case "quantity":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockMovements"
                        SET "QuantityDelta" = -2
                        WHERE "Id" = {consumed.MovementId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                case "terminal-time":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockReservations"
                        SET "TerminalAtUtc" = "TerminalAtUtc" + interval '1 minute'
                        WHERE "Id" = {consumed.ReservationId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                case "actor":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockReservations"
                        SET "TerminalActor" = 'other-actor'
                        WHERE "Id" = {consumed.ReservationId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                case "reason":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockReservations"
                        SET "TerminalReason" = 'other-reason'
                        WHERE "Id" = {consumed.ReservationId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                case "reference":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockReservations"
                        SET "TerminalReference" = 'other-reference'
                        WHERE "Id" = {consumed.ReservationId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                case "version-ahead":
                    await db.Database.ExecuteSqlInterpolatedAsync(
                        $"""
                        UPDATE "StockMovements"
                        SET "ResultingInventoryVersion" = 4
                        WHERE "Id" = {consumed.MovementId}
                        """,
                        TestContext.Current.CancellationToken);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(corruption));
            }
        }

        var sessionFactory = factory.Services
            .GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            session.ExecuteOnceAsync<string>(async cancellationToken =>
            {
                Assert.NotNull(await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken));
                await session.FindReservationAsync(
                    consumed.ReservationId,
                    cancellationToken);
                return Result<string>.Success("invalid");
            }, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task NamedMovementPrimaryKeyViolationHasStablePersistenceClassification()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedInventoryAsync(factory, "pk-first", 2);
        var second = await SeedInventoryAsync(factory, "pk-second", 2);
        var operationId = Guid.NewGuid();
        Assert.True((await ReceiveAsync(factory, first, operationId, 1, 1)).Result.IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondItem = await db.InventoryItems.SingleAsync(
            item => item.Id == second.InventoryId,
            TestContext.Current.CancellationToken);
        var duplicate = secondItem.ReceiveStock(
            operationId, 1, "รับเข้า", "duplicate-pk", 1,
            UtcNow.AddMinutes(1), "admin");
        db.StockMovements.Add(duplicate);
        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => db.SaveChangesAsync(TestContext.Current.CancellationToken));

        Assert.Equal(
            new PersistenceFailure(
                PersistenceFailureTarget.StockMovement,
                PersistenceFailureKind.DuplicateOperation),
            scope.ServiceProvider
                .GetRequiredService<IPersistenceFailureClassifier>()
                .Classify(exception));
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
    private static readonly Error TestFailure =
        new("Test.Failure", "ย้อนกลับ", ErrorType.Conflict);
    private static readonly Error Stale =
        new("Test.Stale", "ข้อมูลเก่า", ErrorType.Conflict);
    private static readonly Error Insufficient =
        new("Test.Insufficient", "สินค้าไม่พอ", ErrorType.Conflict);
    private static readonly Error OperationConflict =
        new("Test.OperationConflict", "หลักฐานซ้ำไม่ตรงกัน", ErrorType.Conflict);
    private static readonly Error TransitionInvalid =
        new("Test.TransitionInvalid", "สถานะเปลี่ยนไม่ได้", ErrorType.Conflict);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private ServiceProvider CreateProvider(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = postgreSql.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(
            new InterceptedContextFactory(postgreSql.ConnectionString, interceptors));
        return services.BuildServiceProvider();
    }

    private static async Task<InventoryMutationExecution<string>> ReceiveAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        Guid operationId,
        long expectedVersion,
        int quantity,
        string reason = "รับสินค้า",
        string reference = "receive-session",
        string actor = "admin")
    {
        var withEvidence = await ReceiveWithEvidenceAsync(
            factory, seeded, operationId, expectedVersion, quantity,
            reason, reference, actor);
        return new InventoryMutationExecution<string>(
            withEvidence.Result.IsSuccess
                ? Result<string>.Success(
                    withEvidence.Result.Value.Changed ? "changed" : "unchanged")
                : Result<string>.Failure(withEvidence.Result.Error),
            withEvidence.CommitOutcome,
            withEvidence.CommitFailure,
            withEvidence.CleanupFailureTypes);
    }

    private static async Task<InventoryMutationExecution<ReceiveAttempt>> ReceiveWithEvidenceAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        Guid operationId,
        long expectedVersion,
        int quantity,
        string reason = "รับสินค้า",
        string reference = "receive-session",
        string actor = "admin")
    {
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        InventoryMutationEvidence? intendedEvidence = null;
        try
        {
            return await session.ExecuteOnceAsync<ReceiveAttempt>(async cancellationToken =>
            {
                var item = await session.LockInventoryAsync(
                    seeded.InventoryId, seeded.ProductId, cancellationToken);
                if (item is null)
                {
                    return Result<ReceiveAttempt>.Failure(TestFailure);
                }

                var intent = InventoryOperationIntent.Create(
                    operationId,
                    seeded.InventoryId,
                    seeded.ProductId,
                    StockMovementType.Received,
                    quantity,
                    expectedVersion,
                    reason,
                    reference,
                    actor);
                var existing = await session.FindMovementAsync(operationId, cancellationToken);
                if (existing is not null)
                {
                    return intent.Matches(existing)
                        ? Result<ReceiveAttempt>.Success(new ReceiveAttempt(false, null))
                        : Result<ReceiveAttempt>.Failure(OperationConflict);
                }

                if (item.Version != expectedVersion)
                {
                    return Result<ReceiveAttempt>.Failure(Stale);
                }

                var movement = item.ReceiveStock(
                    operationId, quantity, reason, reference,
                    expectedVersion,
                    UtcNow.AddMinutes(expectedVersion).AddTicks(7),
                    actor);
                session.Add(movement);
                intendedEvidence = InventoryMutationEvidence.Capture(item, movement);
                return Result<ReceiveAttempt>.Success(new ReceiveAttempt(
                    true,
                    intendedEvidence));
            }, TestContext.Current.CancellationToken);
        }
        catch (DbUpdateException exception) when (
            factory.Services.GetRequiredService<IPersistenceFailureClassifier>()
                .Classify(exception)
            == new PersistenceFailure(
                PersistenceFailureTarget.StockMovement,
                PersistenceFailureKind.DuplicateOperation))
        {
            var evidence = intendedEvidence
                ?? throw new InvalidOperationException(
                    "A movement-PK collision must retain intended evidence.",
                    exception);
            var resolver = new InventoryCommitOutcomeResolver(
                NullLogger<InventoryCommitOutcomeResolver>.Instance);
            var result = await resolver.ResolveOperationCollisionAsync(
                token => sessionFactory.VerifyCommitAsync(evidence, token),
                authoritative => new ReceiveAttempt(false, authoritative),
                OperationConflict,
                "ReceiveStock",
                TestContext.Current.CancellationToken);
            return new InventoryMutationExecution<ReceiveAttempt>(
                result,
                InventoryCommitOutcome.DefinitelyRolledBack);
        }
    }

    private static async Task<InventoryMutationExecution<Guid>> ReserveAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        Guid reservationId,
        long expectedVersion,
        int quantity)
    {
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        return await session.ExecuteOnceAsync<Guid>(async cancellationToken =>
        {
            var item = await session.LockInventoryAsync(
                seeded.InventoryId, seeded.ProductId, cancellationToken);
            if (item is null)
            {
                return Result<Guid>.Failure(TestFailure);
            }

            if (item.Version != expectedVersion)
            {
                return Result<Guid>.Failure(Stale);
            }

            try
            {
                var reservation = item.Reserve(
                    reservationId, Guid.NewGuid(), quantity,
                    UtcNow.AddMinutes(expectedVersion),
                    UtcNow.AddHours(1),
                    "รอชำระ", $"reserve-{reservationId:N}", expectedVersion, "system");
                session.Add(reservation);
                return Result<Guid>.Success(reservation.Id);
            }
            catch (InventoryRuleException exception) when (
                exception.Rule == InventoryRule.InsufficientReservableQuantity)
            {
                return Result<Guid>.Failure(Insufficient);
            }
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<InventoryMutationExecution<string>> AdjustAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        Guid operationId,
        long expectedVersion,
        int delta)
    {
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        return await session.ExecuteOnceAsync<string>(async cancellationToken =>
        {
            var item = await session.LockInventoryAsync(
                seeded.InventoryId, seeded.ProductId, cancellationToken);
            if (item is null || item.Version != expectedVersion)
            {
                return Result<string>.Failure(Stale);
            }

            try
            {
                var movement = item.AdjustStock(
                    operationId, delta, "ตรวจนับ", "adjust-session", expectedVersion,
                    UtcNow.AddMinutes(expectedVersion), "admin");
                session.Add(movement);
                return Result<string>.Success("changed");
            }
            catch (InventoryRuleException exception) when (
                exception.Rule == InventoryRule.InsufficientOnHand)
            {
                return Result<string>.Failure(Insufficient);
            }
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<InventoryMutationExecution<string>> TransitionAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded,
        Guid reservationId,
        bool consume)
    {
        var sessionFactory = factory.Services.GetRequiredService<IInventoryMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        return await session.ExecuteOnceAsync<string>(async cancellationToken =>
        {
            var item = await session.LockInventoryAsync(
                seeded.InventoryId, seeded.ProductId, cancellationToken);
            var reservation = await session.FindReservationAsync(reservationId, cancellationToken);
            if (item is null || reservation is null)
            {
                return Result<string>.Failure(TestFailure);
            }

            try
            {
                var result = consume
                    ? item.ConsumeReservation(
                        reservation, Guid.NewGuid(), "ชำระแล้ว", "consume-race",
                        item.Version, UtcNow.AddMinutes(3), "system")
                    : item.ReleaseReservation(
                        reservation, "ยกเลิก", "release-race",
                        item.Version, UtcNow.AddMinutes(3), "system");
                if (result.Movement is not null)
                {
                    session.Add(result.Movement);
                }

                return Result<string>.Success("changed");
            }
            catch (InventoryRuleException exception) when (
                exception.Rule is InventoryRule.ReservationTransitionInvalid
                    or InventoryRule.ReservationEvidenceConflict)
            {
                return Result<string>.Failure(TransitionInvalid);
            }
        }, TestContext.Current.CancellationToken);
    }

    private static async Task<Guid> SeedProductAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
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
        db.Brands.Add(brand);
        db.Products.Add(product);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return product.Id;
    }

    private static async Task<SeededInventory> SeedInventoryAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        int stock)
    {
        var productId = await SeedProductAsync(factory, suffix);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var creation = InventoryItem.Create(
            Guid.NewGuid(), productId, Guid.NewGuid(), stock,
            "สินค้าเริ่มต้น", $"initial-{suffix}", UtcNow, "test");
        db.InventoryItems.Add(creation.Item);
        db.StockMovements.Add(creation.InitialMovement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededInventory(creation.Item.Id, productId);
    }

    private static async Task<Guid> SeedCrossWiredConsumedReservationAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var first = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, UtcNow.AddMinutes(1), UtcNow.AddHours(1),
            "รอชำระ", "cross-a", item.Version, "system");
        db.StockReservations.Add(first);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var second = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, UtcNow.AddMinutes(2), UtcNow.AddHours(1),
            "รอชำระ", "cross-b", item.Version, "system");
        db.StockReservations.Add(second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var firstResult = item.ConsumeReservation(
            first, Guid.NewGuid(), "ชำระ", "consume-a", item.Version,
            UtcNow.AddMinutes(3), "system");
        db.StockMovements.Add(firstResult.Movement!);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var secondResult = item.ConsumeReservation(
            second, Guid.NewGuid(), "ชำระ", "consume-b", item.Version,
            UtcNow.AddMinutes(4), "system");
        db.StockMovements.Add(secondResult.Movement!);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "StockReservations"
            SET "Status" = 'Released',
                "ConsumedMovementId" = NULL
            WHERE "Id" = {first.Id}
            """,
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "StockReservations"
            SET "ConsumedMovementId" = {firstResult.Movement!.Id}
            WHERE "Id" = {second.Id}
            """,
            TestContext.Current.CancellationToken);
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "StockReservations"
            SET "Status" = 'Consumed',
                "ConsumedMovementId" = {secondResult.Movement!.Id}
            WHERE "Id" = {first.Id}
            """,
            TestContext.Current.CancellationToken);
        return first.Id;
    }

    private static async Task<Guid> SeedActiveReservationWithConsumeMovementAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var reservation = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, UtcNow.AddMinutes(1), UtcNow.AddHours(1),
            "รอชำระ", "active-with-consume", item.Version, "system");
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var consumed = item.ConsumeReservation(
            reservation, Guid.NewGuid(), "ชำระ", "consume-active", item.Version,
            UtcNow.AddMinutes(2), "system");
        db.StockMovements.Add(consumed.Movement!);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "StockReservations"
            SET "Status" = 'Active',
                "TerminalAtUtc" = NULL,
                "TerminalActor" = NULL,
                "TerminalReason" = NULL,
                "TerminalReference" = NULL,
                "ConsumedMovementId" = NULL
            WHERE "Id" = {reservation.Id}
            """,
            TestContext.Current.CancellationToken);
        return reservation.Id;
    }

    private static async Task<ConsumedEvidence> SeedConsumedReservationAsync(
        ToyStoreWebApplicationFactory factory,
        SeededInventory seeded)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var reservation = item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 1, UtcNow.AddMinutes(1), UtcNow.AddHours(1),
            "รอชำระ", "consume-reciprocal", item.Version, "system");
        db.StockReservations.Add(reservation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var consumed = item.ConsumeReservation(
            reservation, Guid.NewGuid(), "ชำระ", "consume-reciprocal", item.Version,
            UtcNow.AddMinutes(2), "system");
        db.StockMovements.Add(consumed.Movement!);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new ConsumedEvidence(reservation.Id, consumed.Movement!.Id);
    }

    private sealed record ReceiveAttempt(
        bool Changed,
        InventoryMutationEvidence? Evidence);

    private sealed record ConsumedEvidence(Guid ReservationId, Guid MovementId);

    private sealed record SeededInventory(Guid InventoryId, Guid ProductId);

    private sealed class InterceptedContextFactory(
        string connectionString,
        params IInterceptor[] interceptors)
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptors)
                .Options;

        public ApplicationDbContext CreateDbContext() => new(options);
    }

    private sealed class SplitLockInsertInterceptor(Func<Task> insertAsync)
        : DbCommandInterceptor
    {
        private int invoked;

        public override async ValueTask<int> NonQueryExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("SELECT 1", StringComparison.Ordinal)
                && command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
                && Interlocked.Exchange(ref invoked, 1) == 0)
            {
                await insertAsync();
            }

            return result;
        }
    }

    private sealed class ForUpdateAttemptInterceptor : DbCommandInterceptor
    {
        private int attempts;

        public TaskCompletionSource SecondAttempted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("FOR UPDATE", StringComparison.Ordinal)
                && Interlocked.Increment(ref attempts) == 2)
            {
                SecondAttempted.TrySetResult();
            }

            return ValueTask.FromResult(result);
        }
    }

    private sealed class CommitAcknowledgementFailureInterceptor : DbTransactionInterceptor
    {
        public int InvocationCount { get; private set; }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromException(new InjectedCommitAcknowledgementException());
        }
    }

    private sealed class CommitAcknowledgementCancellationInterceptor(
        OperationCanceledException cancellation)
        : DbTransactionInterceptor
    {
        public int InvocationCount { get; private set; }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromException(cancellation);
        }
    }

    private sealed class InjectedCommitAcknowledgementException : Exception;
}
