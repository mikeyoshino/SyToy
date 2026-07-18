using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

public sealed class StartupMigrationTests
{
    [Fact]
    public async Task EmptyDatabaseStartupAppliesIdentityAndCatalogMigrations()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        response.EnsureSuccessStatusCode();
        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.True(await TableExistsAsync(connection, "__EFMigrationsHistory"));
        foreach (var identityTable in ExpectedIdentityTables)
        {
            Assert.True(
                await TableExistsAsync(connection, identityTable),
                $"Expected Identity v2 table '{identityTable}'.");
        }

        Assert.True(await ColumnExistsAsync(
            connection,
            "AspNetUsers",
            "MustChangePassword"));
        foreach (var catalogTable in ExpectedCatalogTables)
        {
            Assert.True(
                await TableExistsAsync(connection, catalogTable),
                $"Expected catalog table '{catalogTable}'.");
        }

        Assert.Equal(
            ExpectedMigrationCount(postgreSql.ConnectionString),
            await CountAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(2, await CountAsync(connection, "SELECT COUNT(*) FROM \"ProductCategories\";"));
        Assert.Equal(3, await CountAsync(connection, "SELECT COUNT(*) FROM \"Universes\";"));
        Assert.True(await ColumnExistsAsync(connection, "Brands", "Version"));
        Assert.True(await ColumnExistsAsync(connection, "Universes", "Version"));
        Assert.True(await ColumnExistsAsync(connection, "Products", "Version"));
        Assert.Equal(3, await CountAsync(connection, "SELECT COUNT(*) FROM \"Universes\" WHERE \"Version\" = 1;"));
    }

    [Fact]
    public async Task ExistingDatabaseStartupIsIdempotentAndDoesNotDuplicateRoles()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();

        await using (var firstFactory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString))
        {
            using var firstClient = firstFactory.CreateClient();
            using var response = await firstClient.GetAsync(
                "/health/ready",
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        await using (var secondFactory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString))
        {
            using var secondClient = secondFactory.CreateClient();
            using var response = await secondClient.GetAsync(
                "/health/ready",
                TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(
            ExpectedMigrationCount(postgreSql.ConnectionString),
            await CountAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(2, await CountAsync(connection, "SELECT COUNT(*) FROM \"AspNetRoles\";"));
    }

    [Fact]
    public async Task IdentityOnlyDatabaseStartupAppliesCatalogDeltaOnce()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260716183355_InitialIdentity",
                TestContext.Current.CancellationToken);
        }

        await using (var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString))
        {
            using var client = factory.CreateClient();
            using var response = await client.GetAsync("/health/ready", TestContext.Current.CancellationToken);
            response.EnsureSuccessStatusCode();
        }

        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.True(await TableExistsAsync(connection, "Products"));
        Assert.Equal(
            ExpectedMigrationCount(postgreSql.ConnectionString),
            await CountAsync(connection, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
    }

    [Fact]
    public async Task CartResultReplayMigrationBackfillsExistingMergeRowsBeforeAddingConstraints()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260717123444_AddCartOperationIdempotency",
                TestContext.Current.CancellationToken);
        }

        await using (var connection = new NpgsqlConnection(postgreSql.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                INSERT INTO "AspNetUsers"
                    ("Id", "MustChangePassword", "EmailConfirmed", "PhoneNumberConfirmed",
                     "TwoFactorEnabled", "LockoutEnabled", "AccessFailedCount")
                VALUES ('legacy-cart-customer', false, false, false, false, false, 0);

                INSERT INTO "Carts" ("Id", "CustomerId", "CreatedAtUtc", "UpdatedAtUtc", "Version")
                VALUES ('10000000-0000-0000-0000-000000000001', 'legacy-cart-customer',
                        '2026-07-17T06:00:00Z', '2026-07-17T06:00:00Z', 1);

                INSERT INTO "CartOperations"
                    ("Id", "CartId", "Type", "IntentFingerprint", "ResultingCartVersion", "OccurredAtUtc")
                VALUES ('20000000-0000-0000-0000-000000000001',
                        '10000000-0000-0000-0000-000000000001', 'Merge',
                        'aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa', 1,
                        '2026-07-17T06:00:00Z');
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260717124452_AddCartOperationResultReplay",
                TestContext.Current.CancellationToken);
        }

        await using var verified = new NpgsqlConnection(postgreSql.ConnectionString);
        await verified.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, await CountAsync(verified,
            """
            SELECT COUNT(*) FROM "CartOperations"
            WHERE "Type" = 'Merge'
              AND "ResultingTotalQuantity" = 0
              AND "ResultData" = '{"RejectedItems":[],"ClampedItems":[]}'::jsonb;
            """));
        Assert.Equal(9, await CountAsync(verified, "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
    }

    [Fact]
    public async Task ConflictingSchemaPreventsApplicationStartup()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();

        await using (var connection = new NpgsqlConnection(postgreSql.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = "CREATE TABLE \"AspNetUsers\" (\"Id\" text PRIMARY KEY);";
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient());
    }

    [Fact]
    public void UnreachableDatabasePreventsApplicationStartup()
    {
        const string connectionString =
            "Host=127.0.0.1;Port=1;Database=toystore_unavailable_test;Username=toystore;Password=not-a-secret;Timeout=1;Command Timeout=1;Pooling=false";
        using var factory = new ToyStoreWebApplicationFactory(connectionString);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
    }

    [Fact]
    public void ProductionSourceNeverUsesEnsureCreated()
    {
        var repositoryRoot = FindRepositoryRoot();
        var source = string.Join(
            Environment.NewLine,
            Directory.GetFiles(
                    Path.Combine(repositoryRoot, "src"),
                    "*.cs",
                    SearchOption.AllDirectories)
                .Select(File.ReadAllText));

        Assert.DoesNotContain("EnsureCreated", source, StringComparison.Ordinal);
    }

    private static long ExpectedMigrationCount(string connectionString)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        using var db = new ApplicationDbContext(options);

        return db.Database.GetMigrations().LongCount();
    }

    private static async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string tableName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.tables
                WHERE table_schema = 'public' AND table_name = @tableName
            );
            """;
        command.Parameters.AddWithValue("tableName", tableName);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<bool> ColumnExistsAsync(
        NpgsqlConnection connection,
        string tableName,
        string columnName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT EXISTS (
                SELECT 1
                FROM information_schema.columns
                WHERE table_schema = 'public'
                  AND table_name = @tableName
                  AND column_name = @columnName
            );
            """;
        command.Parameters.AddWithValue("tableName", tableName);
        command.Parameters.AddWithValue("columnName", columnName);
        return (bool)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static async Task<long> CountAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private static readonly string[] ExpectedIdentityTables =
    [
        "AspNetRoleClaims",
        "AspNetRoles",
        "AspNetUserClaims",
        "AspNetUserLogins",
        "AspNetUserRoles",
        "AspNetUsers",
        "AspNetUserTokens",
    ];

    private static readonly string[] ExpectedCatalogTables =
    [
        "Brands",
        "Characters",
        "InventoryItems",
        "MediaCleanupEntries",
        "ProductCategories",
        "ProductCharacters",
        "ProductImages",
        "Products",
        "PreOrderCapacities",
        "PreOrderCapacityMovements",
        "PreOrderCapacityReservations",
        "StockMovements",
        "StockReservations",
        "Universes",
    ];
}
