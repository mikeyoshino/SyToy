using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InventoryPersistenceTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task InitialZeroStockRoundTripsWithExactlyOneImmutableMovement()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "zero", initialStock: 0);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var item = await db.InventoryItems.AsNoTracking().SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var movement = await db.StockMovements.AsNoTracking().SingleAsync(
            value => value.InventoryItemId == seeded.InventoryId,
            TestContext.Current.CancellationToken);

        Assert.Equal(seeded.ProductId, item.ProductId);
        Assert.Equal(0, item.OnHandQuantity);
        Assert.Equal(0, item.HeldQuantity);
        Assert.Equal(1, item.Version);
        Assert.Equal(StockMovementType.InitialStock, movement.Type);
        Assert.Equal(0, movement.QuantityDelta);
        Assert.Equal(0, movement.ResultingOnHandQuantity);
        Assert.Equal(1, movement.ResultingInventoryVersion);
        Assert.Null(movement.ReservationId);
    }

    [Fact]
    public async Task ExactExpiryAndTerminalReservationLifecycleRoundTrip()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "expiry", initialStock: 5);
        var reservedAt = UtcNow.AddMinutes(1);
        var expiresAt = reservedAt.AddMinutes(30);
        Guid reservationId;

        await using (var reserveScope = factory.Services.CreateAsyncScope())
        {
            var db = reserveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = item.Reserve(
                Guid.NewGuid(),
                Guid.NewGuid(),
                2,
                reservedAt,
                expiresAt,
                "รอชำระเงิน",
                "checkout-expiry",
                item.Version,
                "system");
            reservationId = reservation.Id;
            db.StockReservations.Add(reservation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var expiryScope = factory.Services.CreateAsyncScope())
        {
            var db = expiryScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = await db.StockReservations.SingleAsync(
                value => value.Id == reservationId,
                TestContext.Current.CancellationToken);
            Assert.False(reservation.IsEffectiveActiveAt(expiresAt));
            var result = item.ExpireReservation(
                reservation,
                "หมดเวลาชำระเงิน",
                "expire-exact",
                item.Version,
                expiresAt,
                "system");
            Assert.True(result.Changed);
            Assert.Null(result.Movement);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedItem = await verification.InventoryItems.AsNoTracking().SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var savedReservation = await verification.StockReservations.AsNoTracking().SingleAsync(
            value => value.Id == reservationId,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, savedItem.HeldQuantity);
        Assert.Equal(3, savedItem.Version);
        Assert.Equal(StockReservationStatus.Expired, savedReservation.Status);
        Assert.Equal(expiresAt, savedReservation.TerminalAtUtc);
        Assert.Equal("expire-exact", savedReservation.TerminalReference);
        Assert.Null(savedReservation.ConsumedMovementId);
    }

    [Fact]
    public async Task ConsumedReservationAndCompositeLinkedMovementRoundTripWithoutReverseForeignKey()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "consume", initialStock: 5);
        var reservedAt = UtcNow.AddMinutes(1);
        var movementId = Guid.NewGuid();
        Guid reservationId;

        await using (var reserveScope = factory.Services.CreateAsyncScope())
        {
            var db = reserveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = item.Reserve(
                Guid.NewGuid(), Guid.NewGuid(), 2, reservedAt, reservedAt.AddMinutes(30),
                "รอชำระ", "checkout-consume", item.Version, "system");
            reservationId = reservation.Id;
            db.StockReservations.Add(reservation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var consumeScope = factory.Services.CreateAsyncScope())
        {
            var db = consumeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = await db.StockReservations.SingleAsync(
                value => value.Id == reservationId,
                TestContext.Current.CancellationToken);
            var result = item.ConsumeReservation(
                reservation,
                movementId,
                "ชำระเงินสำเร็จ",
                "payment-consume",
                item.Version,
                reservedAt.AddMinutes(2),
                "system");
            db.StockMovements.Add(Assert.IsType<StockMovement>(result.Movement));
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedReservation = await verification.StockReservations.AsNoTracking().SingleAsync(
            value => value.Id == reservationId,
            TestContext.Current.CancellationToken);
        var savedMovement = await verification.StockMovements.AsNoTracking().SingleAsync(
            value => value.Id == movementId,
            TestContext.Current.CancellationToken);
        Assert.Equal(StockReservationStatus.Consumed, savedReservation.Status);
        Assert.Equal(movementId, savedReservation.ConsumedMovementId);
        Assert.Equal(reservationId, savedMovement.ReservationId);
        Assert.Equal(-2, savedMovement.QuantityDelta);
        Assert.Equal(3, savedMovement.ResultingOnHandQuantity);
        Assert.Equal(3, savedMovement.ResultingInventoryVersion);
    }

    [Fact]
    public async Task CounterMovementUniquenessAndDeleteRestrictionsRejectInvalidDirectWrites()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "constraints", initialStock: 5);
        await using var connection = await OpenAsync();

        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_InventoryItems_HeldQuantity_Bounds",
            connection,
            $"""
            INSERT INTO "InventoryItems"
                ("Id", "ProductId", "OnHandQuantity", "HeldQuantity", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy", "Version")
            SELECT '{Guid.NewGuid()}', "ProductId", 0, 1, "CreatedAtUtc", 'test', "UpdatedAtUtc", 'test', 1
            FROM "InventoryItems" WHERE "Id" = '{seeded.InventoryId}';
            """);
        await AssertConstraintAsync(
            PostgresErrorCodes.UniqueViolation,
            "UX_InventoryItems_ProductId",
            connection,
            $"""
            INSERT INTO "InventoryItems"
                ("Id", "ProductId", "OnHandQuantity", "HeldQuantity", "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy", "Version")
            SELECT '{Guid.NewGuid()}', "ProductId", 0, 0, "CreatedAtUtc", 'test', "UpdatedAtUtc", 'test', 1
            FROM "InventoryItems" WHERE "Id" = '{seeded.InventoryId}';
            """);
        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_StockMovements_Quantity_Evidence",
            connection,
            $"""
            INSERT INTO "StockMovements"
                ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
            VALUES
                ('{Guid.NewGuid()}', '{seeded.InventoryId}', '{seeded.ProductId}', 'Received', 0, 5, 2, 'bad', 'bad-shape', 'test', TIMESTAMPTZ '2026-07-17 00:01:00+00', NULL);
            """);
        await ExecuteAsync(
            connection,
            $"""
            INSERT INTO "StockMovements"
                ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
            VALUES
                ('{Guid.NewGuid()}', '{seeded.InventoryId}', '{seeded.ProductId}', 'Received', 1, 6, 2, 'receive', 'version-two', 'test', TIMESTAMPTZ '2026-07-17 00:01:00+00', NULL);
            """);
        await AssertConstraintAsync(
            PostgresErrorCodes.UniqueViolation,
            "UX_StockMovements_InventoryItemId_ResultingInventoryVersion",
            connection,
            $"""
            INSERT INTO "StockMovements"
                ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
            VALUES
                ('{Guid.NewGuid()}', '{seeded.InventoryId}', '{seeded.ProductId}', 'Received', 1, 6, 2, 'duplicate version', 'duplicate-version', 'test', TIMESTAMPTZ '2026-07-17 00:02:00+00', NULL);
            """);
        await AssertConstraintAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_InventoryItems_Products_ProductId",
            connection,
            $"DELETE FROM \"Products\" WHERE \"Id\" = '{seeded.ProductId}';");
    }

    [Fact]
    public async Task CompositeOwnershipAndReservationLifecycleRejectCrossItemEvidence()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedInventoryAsync(factory, "owner-a", initialStock: 5);
        var second = await SeedInventoryAsync(factory, "owner-b", initialStock: 5);
        var reservationId = Guid.NewGuid();
        await using var connection = await OpenAsync();

        await AssertConstraintAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_StockReservations_InventoryItems_InventoryItemId_ProductId",
            connection,
            ActiveReservationSql(Guid.NewGuid(), first.InventoryId, second.ProductId));
        await ExecuteAsync(
            connection,
            ActiveReservationSql(reservationId, first.InventoryId, first.ProductId));
        await AssertConstraintAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_StockMovements_StockReservations_ReservationId_InventoryIte~",
            connection,
            $"""
            INSERT INTO "StockMovements"
                ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
            VALUES
                ('{Guid.NewGuid()}', '{second.InventoryId}', '{second.ProductId}', 'ReservationConsumed', -1, 4, 2, 'consume', 'cross-owner', 'system', TIMESTAMPTZ '2026-07-17 00:02:00+00', '{reservationId}');
            """);
        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_StockReservations_Lifecycle_Evidence",
            connection,
            $"UPDATE \"StockReservations\" SET \"Status\" = 'Released' WHERE \"Id\" = '{reservationId}';");
        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_StockReservations_Terminal_Chronology",
            connection,
            $"""
            UPDATE "StockReservations"
            SET "Status" = 'Expired',
                "TerminalAtUtc" = "ExpiresAtUtc" - INTERVAL '1 second',
                "TerminalActor" = 'system',
                "TerminalReason" = 'early',
                "TerminalReference" = 'early-expiry'
            WHERE "Id" = '{reservationId}';
            """);
    }

    [Fact]
    public async Task NonexistentConsumedMovementEvidenceIsRejectedByReverseForeignKey()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "missing-consume", initialStock: 5);
        var reservationId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            ActiveReservationSql(reservationId, seeded.InventoryId, seeded.ProductId));

        await AssertConstraintAsync(
            PostgresErrorCodes.ForeignKeyViolation,
            "FK_StockReservations_StockMovements_ConsumedMovementId",
            connection,
            ConsumeReservationSql(reservationId, Guid.NewGuid(), "missing-movement"));
    }

    [Fact]
    public async Task OneReservationCannotOwnTwoConsumeMovements()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "duplicate-consume", initialStock: 5);
        var reservationId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            ActiveReservationSql(reservationId, seeded.InventoryId, seeded.ProductId));
        await ExecuteAsync(
            connection,
            ConsumeMovementSql(
                Guid.NewGuid(), reservationId, seeded.InventoryId, seeded.ProductId, version: 2));

        await AssertConstraintAsync(
            PostgresErrorCodes.UniqueViolation,
            "UX_StockMovements_ReservationId",
            connection,
            ConsumeMovementSql(
                Guid.NewGuid(), reservationId, seeded.InventoryId, seeded.ProductId, version: 3));
    }

    [Fact]
    public async Task OneConsumeMovementCannotTerminateTwoReservations()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "duplicate-terminal", initialStock: 5);
        var firstReservationId = Guid.NewGuid();
        var secondReservationId = Guid.NewGuid();
        var movementId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            ActiveReservationSql(firstReservationId, seeded.InventoryId, seeded.ProductId));
        await ExecuteAsync(
            connection,
            ActiveReservationSql(secondReservationId, seeded.InventoryId, seeded.ProductId));
        await ExecuteAsync(
            connection,
            ConsumeMovementSql(
                movementId, firstReservationId, seeded.InventoryId, seeded.ProductId, version: 2));
        await ExecuteAsync(
            connection,
            ConsumeReservationSql(firstReservationId, movementId, "first-terminal"));

        await AssertConstraintAsync(
            PostgresErrorCodes.UniqueViolation,
            "UX_StockReservations_ConsumedMovementId",
            connection,
            ConsumeReservationSql(secondReservationId, movementId, "second-terminal"));
    }

    [Fact]
    public async Task SameInventoryCrossWiredConsumeEvidenceRemainsDatabaseBoundaryForSessionValidation()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "cross-wire", initialStock: 5);
        var firstReservationId = Guid.NewGuid();
        var secondReservationId = Guid.NewGuid();
        var firstMovementId = Guid.NewGuid();
        var secondMovementId = Guid.NewGuid();
        await using var connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            ActiveReservationSql(firstReservationId, seeded.InventoryId, seeded.ProductId));
        await ExecuteAsync(
            connection,
            ActiveReservationSql(secondReservationId, seeded.InventoryId, seeded.ProductId));
        await ExecuteAsync(
            connection,
            ConsumeMovementSql(
                firstMovementId, firstReservationId, seeded.InventoryId, seeded.ProductId, version: 2));
        await ExecuteAsync(
            connection,
            ConsumeMovementSql(
                secondMovementId, secondReservationId, seeded.InventoryId, seeded.ProductId, version: 3));

        await ExecuteAsync(
            connection,
            ConsumeReservationSql(firstReservationId, secondMovementId, "cross-wire-a"));
        await ExecuteAsync(
            connection,
            ConsumeReservationSql(secondReservationId, firstMovementId, "cross-wire-b"));

        Assert.Equal(
            2L,
            await ScalarAsync(
                connection,
                $"""
                SELECT COUNT(*)
                FROM "StockReservations"
                WHERE "Id" IN ('{firstReservationId}', '{secondReservationId}')
                  AND "Status" = 'Consumed';
                """));
    }

    [Fact]
    public async Task NonInitialMovementAtVersionOneIsRejectedEvenWithoutInitialEvidenceRow()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "version-one", initialStock: 5);
        await using var connection = await OpenAsync();
        await ExecuteAsync(
            connection,
            $"DELETE FROM \"StockMovements\" WHERE \"InventoryItemId\" = '{seeded.InventoryId}';");

        await AssertConstraintAsync(
            PostgresErrorCodes.CheckViolation,
            "CK_StockMovements_Version_MatchesType",
            connection,
            $"""
            INSERT INTO "StockMovements"
                ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
            VALUES
                ('{Guid.NewGuid()}', '{seeded.InventoryId}', '{seeded.ProductId}', 'Received', 1, 6, 1, 'receive', 'version-one', 'test', TIMESTAMPTZ '2026-07-17 00:01:00+00', NULL);
            """);
    }

    [Fact]
    public async Task ConsumeSaveFailureRollsBackItemReservationAndMovementAcrossNullableCycle()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedInventoryAsync(factory, "consume-rollback", initialStock: 5);
        var reservedAt = UtcNow.AddMinutes(1);
        Guid reservationId;
        string duplicateBrandName;

        await using (var reserveScope = factory.Services.CreateAsyncScope())
        {
            var db = reserveScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = item.Reserve(
                Guid.NewGuid(), Guid.NewGuid(), 2, reservedAt, reservedAt.AddMinutes(30),
                "รอชำระ", "checkout-rollback", item.Version, "system");
            reservationId = reservation.Id;
            db.StockReservations.Add(reservation);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            duplicateBrandName = await db.Brands
                .Select(brand => brand.DisplayName)
                .FirstAsync(TestContext.Current.CancellationToken);
        }

        var movementId = Guid.NewGuid();
        await using (var consumeScope = factory.Services.CreateAsyncScope())
        {
            var db = consumeScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                value => value.Id == seeded.InventoryId,
                TestContext.Current.CancellationToken);
            var reservation = await db.StockReservations.SingleAsync(
                value => value.Id == reservationId,
                TestContext.Current.CancellationToken);
            var result = item.ConsumeReservation(
                reservation,
                movementId,
                "ชำระเงินสำเร็จ",
                "payment-rollback",
                item.Version,
                reservedAt.AddMinutes(2),
                "system");
            db.StockMovements.Add(Assert.IsType<StockMovement>(result.Movement));
            db.Brands.Add(Brand.Create(
                Guid.NewGuid(),
                duplicateBrandName,
                "Different English Name",
                CatalogSlug.Create("different-rollback-brand"),
                UtcNow,
                "test"));

            await Assert.ThrowsAsync<DbUpdateException>(
                () => db.SaveChangesAsync(TestContext.Current.CancellationToken));
        }

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verification = verificationScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var savedItem = await verification.InventoryItems.AsNoTracking().SingleAsync(
            value => value.Id == seeded.InventoryId,
            TestContext.Current.CancellationToken);
        var savedReservation = await verification.StockReservations.AsNoTracking().SingleAsync(
            value => value.Id == reservationId,
            TestContext.Current.CancellationToken);
        Assert.Equal(5, savedItem.OnHandQuantity);
        Assert.Equal(2, savedItem.HeldQuantity);
        Assert.Equal(2, savedItem.Version);
        Assert.Equal(StockReservationStatus.Active, savedReservation.Status);
        Assert.Null(savedReservation.ConsumedMovementId);
        Assert.False(await verification.StockMovements.AnyAsync(
            movement => movement.Id == movementId,
            TestContext.Current.CancellationToken));
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private static async Task<SeededInventory> SeedInventoryAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        int initialStock)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.Create(
            Guid.NewGuid(),
            $"แบรนด์ {suffix}",
            $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"),
            UtcNow,
            "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(),
            $"สินค้า {suffix}",
            $"Product {suffix}",
            "รายละเอียด",
            $"product-{suffix}",
            CatalogSeedIds.ArtToyCategory,
            brand.Id,
            CatalogSeedIds.UnknownUniverse,
            InStockOffer.Create(Money.Create(100)),
            UtcNow,
            "test");
        var creation = InventoryItem.Create(
            Guid.NewGuid(),
            product.Id,
            Guid.NewGuid(),
            initialStock,
            "สินค้าเริ่มต้น",
            $"initial-{suffix}",
            UtcNow,
            "test");
        db.Brands.Add(brand);
        db.Products.Add(product);
        db.InventoryItems.Add(creation.Item);
        db.StockMovements.Add(creation.InitialMovement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededInventory(creation.Item.Id, product.Id);
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        return connection;
    }

    private static string ActiveReservationSql(Guid id, Guid inventoryId, Guid productId) =>
        $"""
        INSERT INTO "StockReservations"
            ("Id", "InventoryItemId", "ProductId", "CheckoutAttemptId", "Quantity", "ReservedAtUtc", "ExpiresAtUtc", "ReserveReason", "ReserveReference", "ReservedBy", "Status", "TerminalAtUtc", "TerminalActor", "TerminalReason", "TerminalReference", "ConsumedMovementId")
        VALUES
            ('{id}', '{inventoryId}', '{productId}', '{Guid.NewGuid()}', 1, TIMESTAMPTZ '2026-07-17 00:01:00+00', TIMESTAMPTZ '2026-07-17 00:31:00+00', 'reserve', 'checkout', 'system', 'Active', NULL, NULL, NULL, NULL, NULL);
        """;

    private static string ConsumeMovementSql(
        Guid id,
        Guid reservationId,
        Guid inventoryId,
        Guid productId,
        long version) =>
        $"""
        INSERT INTO "StockMovements"
            ("Id", "InventoryItemId", "ProductId", "Type", "QuantityDelta", "ResultingOnHandQuantity", "ResultingInventoryVersion", "Reason", "Reference", "Actor", "OccurredAtUtc", "ReservationId")
        VALUES
            ('{id}', '{inventoryId}', '{productId}', 'ReservationConsumed', -1, 4, {version}, 'consume', 'consume-{version}', 'system', TIMESTAMPTZ '2026-07-17 00:02:00+00', '{reservationId}');
        """;

    private static string ConsumeReservationSql(
        Guid reservationId,
        Guid movementId,
        string reference) =>
        $"""
        UPDATE "StockReservations"
        SET "Status" = 'Consumed',
            "TerminalAtUtc" = TIMESTAMPTZ '2026-07-17 00:02:00+00',
            "TerminalActor" = 'system',
            "TerminalReason" = 'consume',
            "TerminalReference" = '{reference}',
            "ConsumedMovementId" = '{movementId}'
        WHERE "Id" = '{reservationId}';
        """;

    private static async Task AssertConstraintAsync(
        string sqlState,
        string constraintName,
        NpgsqlConnection connection,
        string sql)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(
            () => ExecuteAsync(connection, sql));
        Assert.Equal(sqlState, exception.SqlState);
        Assert.Equal(constraintName, exception.ConstraintName);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private sealed record SeededInventory(Guid InventoryId, Guid ProductId);
}
