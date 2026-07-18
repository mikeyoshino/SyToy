using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Respawn;
using Respawn.Graph;
using Testcontainers.PostgreSql;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:17-alpine")
        .WithDatabase("toystore_integration_test")
        .WithUsername("toystore_test")
        .WithPassword("toystore_test_password")
        .Build();
    private Respawner? respawner;

    public string ConnectionString => container.GetConnectionString();

    public static bool IsSafeTestDatabase(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var database = new NpgsqlConnectionStringBuilder(connectionString).Database;

            return !string.IsNullOrWhiteSpace(database)
                && (database.EndsWith("_test", StringComparison.OrdinalIgnoreCase)
                    || database.EndsWith("_integration_test", StringComparison.OrdinalIgnoreCase));
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    public async ValueTask InitializeAsync()
    {
        await container.StartAsync(TestContext.Current.CancellationToken);

        if (!IsSafeTestDatabase(ConnectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL integration fixture refused an unsafe database name.");
        }
    }

    public async ValueTask DisposeAsync()
    {
        await container.DisposeAsync();
    }

    private async Task ResetDatabaseAsync()
    {
        if (!IsSafeTestDatabase(ConnectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL integration fixture refused to reset an unsafe database name.");
        }

        await using var connection = new NpgsqlConnection(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        respawner ??= await Respawner.CreateAsync(
            connection,
            new RespawnerOptions
            {
                DbAdapter = DbAdapter.Postgres,
                SchemasToInclude = ["public"],
                TablesToIgnore = [new Table("__EFMigrationsHistory")],
            });

        await respawner.ResetAsync(connection);
    }

    public async Task StopAsync()
    {
        await container.StopAsync(TestContext.Current.CancellationToken);
    }

    public async Task ResetAsync(IServiceProvider applicationServices)
    {
        ArgumentNullException.ThrowIfNull(applicationServices);

        await ResetDatabaseAsync();
        await using var scope = applicationServices.CreateAsyncScope();
        var initializer = scope.ServiceProvider.GetRequiredService<IIdentityInitializer>();
        await initializer.SeedRolesAsync(TestContext.Current.CancellationToken);
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await CatalogTestSeedRestorer.RestoreAsync(
            dbContext,
            TestContext.Current.CancellationToken);
    }
}
