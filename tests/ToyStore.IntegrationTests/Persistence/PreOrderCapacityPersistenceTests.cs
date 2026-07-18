using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class PreOrderCapacityPersistenceTests(PostgreSqlFixture postgreSql)
{
    private static readonly DateTimeOffset CreatedAtUtc =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static readonly DateTimeOffset CloseAtUtc =
        new(2026, 12, 31, 16, 59, 59, TimeSpan.Zero);

    [Fact]
    public async Task ReserveConsumeAndAfterCloseCancellationRoundTripWithMovementHistory()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedCapacityAsync(factory, "history", totalCapacity: 2);
        var reservedAt = CreatedAtUtc.AddMinutes(1);
        Guid reservationId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var capacity = await db.PreOrderCapacities.SingleAsync(
                item => item.Id == seeded.CapacityId,
                TestContext.Current.CancellationToken);
            var reservation = capacity.Reserve(
                Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
                reservedAt, reservedAt.AddMinutes(32), Guid.NewGuid(),
                "เริ่มชำระมัดจำ", "checkout:history", capacity.Version, "customer-1");
            reservationId = reservation.Reservation.Id;
            db.PreOrderCapacityReservations.Add(reservation.Reservation);
            db.PreOrderCapacityMovements.Add(reservation.Movement);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var capacity = await db.PreOrderCapacities.SingleAsync(
                item => item.Id == seeded.CapacityId,
                TestContext.Current.CancellationToken);
            var reservation = await db.PreOrderCapacityReservations.SingleAsync(
                item => item.Id == reservationId,
                TestContext.Current.CancellationToken);
            var result = capacity.ConsumeReservation(
                reservation, Guid.NewGuid(), "รับมัดจำแล้ว", "payment:history",
                capacity.Version, reservedAt.AddMinutes(2), "stripe-webhook");
            db.PreOrderCapacityMovements.Add(Assert.IsType<PreOrderCapacityMovement>(result.Movement));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var capacity = await db.PreOrderCapacities.SingleAsync(
                item => item.Id == seeded.CapacityId,
                TestContext.Current.CancellationToken);
            var reservation = await db.PreOrderCapacityReservations.SingleAsync(
                item => item.Id == reservationId,
                TestContext.Current.CancellationToken);
            var result = capacity.CancelReservation(
                reservation, Guid.NewGuid(), PreOrderCancellationKind.Customer,
                "ลูกค้ายกเลิกหลังปิดรอบ", "order:history:cancel",
                capacity.Version, CloseAtUtc, "customer-1");
            db.PreOrderCapacityMovements.Add(Assert.IsType<PreOrderCapacityMovement>(result.Movement));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedCapacity = await verification.PreOrderCapacities.AsNoTracking().SingleAsync(
            item => item.Id == seeded.CapacityId,
            TestContext.Current.CancellationToken);
        var savedReservation = await verification.PreOrderCapacityReservations.AsNoTracking().SingleAsync(
            item => item.Id == reservationId,
            TestContext.Current.CancellationToken);
        var movements = await verification.PreOrderCapacityMovements.AsNoTracking()
            .Where(item => item.CapacityId == seeded.CapacityId)
            .OrderBy(item => item.ResultingCapacityVersion)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        Assert.Equal(1, savedCapacity.RemainingQuantity);
        Assert.Equal(0, savedCapacity.HeldQuantity);
        Assert.Equal(0, savedCapacity.CommittedQuantity);
        Assert.Equal(1, savedCapacity.RetiredQuantity);
        Assert.Equal(4, savedCapacity.Version);
        Assert.Equal(PreOrderCapacityReservationStatus.Cancelled, savedReservation.Status);
        Assert.Equal(PreOrderDepositDisposition.Forfeited, savedReservation.DepositDisposition);
        Assert.Equal(
            [
                PreOrderCapacityMovementType.InitialCapacity,
                PreOrderCapacityMovementType.Reserved,
                PreOrderCapacityMovementType.ReservationConsumed,
                PreOrderCapacityMovementType.CancellationRetired,
            ],
            movements.Select(item => item.Type));
        Assert.Equal([1L, 2L, 3L, 4L], movements.Select(item => item.ResultingCapacityVersion));
    }

    [Fact]
    public async Task StaleCapacityUpdateIsRejectedAndLosingEvidenceRollsBack()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedCapacityAsync(factory, "concurrency", totalCapacity: 1);
        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = await firstDb.PreOrderCapacities.SingleAsync(
            item => item.Id == seeded.CapacityId,
            TestContext.Current.CancellationToken);
        var stale = await secondDb.PreOrderCapacities.SingleAsync(
            item => item.Id == seeded.CapacityId,
            TestContext.Current.CancellationToken);
        var firstReservation = first.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-1", 1,
            CreatedAtUtc.AddMinutes(1), CreatedAtUtc.AddMinutes(33), Guid.NewGuid(),
            "reserve", "checkout:first", first.Version, "customer-1");
        firstDb.AddRange(firstReservation.Reservation, firstReservation.Movement);
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var losingReservation = stale.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), "customer-2", 1,
            CreatedAtUtc.AddMinutes(2), CreatedAtUtc.AddMinutes(34), Guid.NewGuid(),
            "reserve", "checkout:losing", stale.Version, "customer-2");
        secondDb.AddRange(losingReservation.Reservation, losingReservation.Movement);

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));
        var postgres = Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(PostgresErrorCodes.UniqueViolation, postgres.SqlState);
        Assert.Equal(
            "UX_PreOrderCapacityMovements_CapacityId_Version",
            postgres.ConstraintName);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var authoritative = await verification.PreOrderCapacities.AsNoTracking().SingleAsync(
            item => item.Id == seeded.CapacityId,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, authoritative.RemainingQuantity);
        Assert.Equal(1, authoritative.HeldQuantity);
        Assert.Equal(2, authoritative.Version);
        Assert.False(await verification.PreOrderCapacityReservations.AnyAsync(
            item => item.Id == losingReservation.Reservation.Id,
            TestContext.Current.CancellationToken));
        Assert.False(await verification.PreOrderCapacityMovements.AnyAsync(
            item => item.Id == losingReservation.Movement.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DatabaseRejectsNegativeAccountingAndDuplicateCapacityPerProduct()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedCapacityAsync(factory, "constraints", totalCapacity: 2);
        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_PreOrderCapacities_QuantityAccounting",
            connection,
            $"UPDATE \"PreOrderCapacities\" SET \"HeldQuantity\" = 3 WHERE \"Id\" = '{seeded.CapacityId}';");
        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_PreOrderCapacities_CloseAfterCreated",
            connection,
            $"UPDATE \"PreOrderCapacities\" SET \"CloseAtUtc\" = \"CreatedAtUtc\" WHERE \"Id\" = '{seeded.CapacityId}';");
        await AssertConstraintAsync(
            PostgresErrorCodes.UniqueViolation,
            "UX_PreOrderCapacities_ProductId",
            connection,
            $"""
            INSERT INTO "PreOrderCapacities"
                ("Id", "ProductId", "TotalCapacity", "HeldQuantity", "CommittedQuantity", "RetiredQuantity", "CloseAtUtc", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy", "Version")
            SELECT '{Guid.NewGuid()}', "ProductId", 2, 0, 0, 0, "CloseAtUtc", "CreatedAtUtc", 'test', "UpdatedAtUtc", 'test', 1
            FROM "PreOrderCapacities" WHERE "Id" = '{seeded.CapacityId}';
            """);
    }

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<SeededCapacity> SeedCapacityAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        int totalCapacity)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), CreatedAtUtc, "test");
        var offer = PreOrderOffer.Create(
            Money.Create(1000), Money.Create(200), new DateOnly(2026, 12, 31),
            EstimatedArrival.Create(1, 2027), totalCapacity, 1, CreatedAtUtc);
        var product = Product.CreatePreOrder(
            Guid.NewGuid(), $"สินค้า {suffix}", $"Product {suffix}", "รายละเอียด",
            $"product-{suffix}", CatalogSeedIds.ArtToyCategory, brand.Id,
            CatalogSeedIds.UnknownUniverse, offer, CreatedAtUtc, "test");
        var creation = PreOrderCapacity.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), offer,
            "เปิด capacity", $"product:{suffix}", CreatedAtUtc, "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.PreOrderCapacities.Add(creation.Capacity);
        db.PreOrderCapacityMovements.Add(creation.Movement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededCapacity(creation.Capacity.Id, product.Id);
    }

    private static async Task AssertConstraintAsync(
        string sqlState,
        string constraintName,
        NpgsqlConnection connection,
        string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(sqlState, exception.SqlState);
        Assert.Equal(constraintName, exception.ConstraintName);
    }

    private sealed record SeededCapacity(Guid CapacityId, Guid ProductId);
}
