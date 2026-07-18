using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using ToyStore.Domain.Catalog;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

public sealed class CatalogCleanupMigrationArtifactTests
{
    [Fact]
    public async Task InventoryFoundationIdempotentScriptAppliesTwiceAfterIdentityAndCatalog()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260717040737_AddCatalogCleanupLedger",
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var sql = await File.ReadAllTextAsync(
            Path.Combine(
                RepositoryRoot(),
                "artifacts",
                "migrations",
                "AddInventoryFoundation.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteAsync(connection, sql);
        await ExecuteAsync(connection, sql);

        Assert.Equal(4L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(1L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'InventoryItems';"));
        Assert.Equal(1L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'StockMovements';"));
        Assert.Equal(1L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'StockReservations';"));
    }

    [Fact]
    public async Task IdempotentDeltaAppliesTwiceAndDefaultsExistingAndNewReferenceVersions()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260716235231_AddCatalogFoundation",
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var existingBrandId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        var existingUniverseId = Guid.Parse("30000000-0000-0000-0000-000000000002");
        await InsertReferenceAsync(connection, "Brands", existingBrandId, "existing-brand");
        await InsertReferenceAsync(connection, "Universes", existingUniverseId, "existing-universe");

        var sql = await File.ReadAllTextAsync(
            Path.Combine(
                RepositoryRoot(),
                "artifacts",
                "migrations",
                "AddCatalogCleanupLedger.sql"),
            TestContext.Current.CancellationToken);

        await ExecuteAsync(connection, sql);
        await ExecuteAsync(connection, sql);

        Assert.Equal(1L, await VersionAsync(connection, "Brands", existingBrandId));
        Assert.Equal(1L, await VersionAsync(connection, "Universes", existingUniverseId));

        var newBrandId = Guid.Parse("30000000-0000-0000-0000-000000000003");
        var newUniverseId = Guid.Parse("30000000-0000-0000-0000-000000000004");
        await InsertReferenceAsync(connection, "Brands", newBrandId, "new-brand");
        await InsertReferenceAsync(connection, "Universes", newUniverseId, "new-universe");

        Assert.Equal(3L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(5L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM \"Universes\" WHERE \"Version\" = 1;"));
        Assert.Equal(1L, await VersionAsync(connection, "Brands", newBrandId));
        Assert.Equal(1L, await VersionAsync(connection, "Universes", newUniverseId));
        Assert.Equal(1L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'MediaCleanupEntries';"));
    }

    [Fact]
    public async Task ProductVersionDeltaAppliesTwiceAndBackfillsExistingProduct()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options;
        await using (var db = new ApplicationDbContext(options))
        {
            await db.GetService<IMigrator>().MigrateAsync(
                "20260717075735_AddInventoryFoundation",
                TestContext.Current.CancellationToken);
        }

        await using var connection = new NpgsqlConnection(postgreSql.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var brandId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        await InsertReferenceAsync(connection, "Brands", brandId, "migration-brand");
        await using (var insert = connection.CreateCommand())
        {
            insert.CommandText = """
                INSERT INTO "Products"
                    ("Id", "DisplayName", "NormalizedDisplayName", "EnglishName",
                     "NormalizedEnglishName", "Description", "Slug", "ProductCategoryId",
                     "BrandId", "UniverseId", "SaleType", "Status", "InStockPrice",
                     "CreatedAtUtc", "CreatedBy", "UpdatedAtUtc", "UpdatedBy")
                VALUES
                    (@id, 'สินค้าก่อน migration', 'สินค้าก่อน MIGRATION', 'Existing Product',
                     'EXISTING PRODUCT', 'รายละเอียด', 'existing-product', @categoryId,
                     @brandId, @universeId, 'InStock', 'Draft', 100,
                     TIMESTAMPTZ '2026-07-17 00:00:00+00', 'test:migration',
                     TIMESTAMPTZ '2026-07-17 00:00:00+00', 'test:migration');
                """;
            insert.Parameters.AddWithValue("id", productId);
            insert.Parameters.AddWithValue("categoryId", CatalogSeedIds.GundamCategory);
            insert.Parameters.AddWithValue("brandId", brandId);
            insert.Parameters.AddWithValue("universeId", CatalogSeedIds.UnknownUniverse);
            await insert.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var sql = await File.ReadAllTextAsync(
            Path.Combine(
                RepositoryRoot(),
                "artifacts",
                "migrations",
                "AddProductVersion.sql"),
            TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, sql);
        await ExecuteAsync(connection, sql);

        Assert.Equal(1L, await VersionAsync(connection, "Products", productId));
        Assert.Equal(5L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM \"__EFMigrationsHistory\";"));
        Assert.Equal(3L, await ScalarAsync(
            connection,
            "SELECT COUNT(*) FROM pg_constraint WHERE conname IN ('CK_Products_Version_Positive', 'CK_Products_Audit_Chronology', 'CK_Products_Lifecycle_Audit');"));
    }

    private static async Task InsertReferenceAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id,
        string slug)
    {
        Assert.True(tableName is "Brands" or "Universes");
        await using var command = connection.CreateCommand();
        command.CommandText = $"""
            INSERT INTO "{tableName}"
                ("Id", "DisplayName", "NormalizedDisplayName", "EnglishName",
                 "NormalizedEnglishName", "Slug", "Status", "CreatedAtUtc",
                 "CreatedBy", "UpdatedAtUtc", "UpdatedBy")
            VALUES
                (@id, @displayName, @normalizedName, @displayName,
                 @normalizedName, @slug, 'Active', TIMESTAMPTZ '2026-07-17 00:00:00+00',
                 'test:migration', TIMESTAMPTZ '2026-07-17 00:00:00+00', 'test:migration');
            """;
        command.Parameters.AddWithValue("id", id);
        command.Parameters.AddWithValue("displayName", slug);
        command.Parameters.AddWithValue("normalizedName", slug.ToUpperInvariant());
        command.Parameters.AddWithValue("slug", slug);
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    private static Task<long> VersionAsync(
        NpgsqlConnection connection,
        string tableName,
        Guid id) =>
        ScalarAsync(
            connection,
            $"SELECT \"Version\" FROM \"{tableName}\" WHERE \"Id\" = '{id}';");

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

    private static string RepositoryRoot()
    {
        for (var current = new DirectoryInfo(AppContext.BaseDirectory);
             current is not null;
             current = current.Parent)
        {
            if (File.Exists(Path.Combine(current.FullName, "ToyStore.sln")))
            {
                return current.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }
}
