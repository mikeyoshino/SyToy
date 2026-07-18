using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ToyStore.IntegrationTests.Infrastructure;

public sealed class PostgreSqlFixtureTests
{
    [Fact]
    public async Task StartsThrowawayPostgreSqlWithSafeDatabaseName()
    {
        await using var fixture = new PostgreSqlFixture();

        await fixture.InitializeAsync();

        Assert.True(PostgreSqlFixture.IsSafeTestDatabase(fixture.ConnectionString));

        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        Assert.Equal("toystore_integration_test", connection.Database);
    }

    [Fact]
    public async Task ResetDeletesRowsOnlyFromTheThrowawayDatabase()
    {
        await using var fixture = new PostgreSqlFixture();
        await fixture.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        await using (var connection = new NpgsqlConnection(fixture.ConnectionString))
        {
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE reset_probe (id integer PRIMARY KEY);
                INSERT INTO reset_probe (id) VALUES (1);
                """;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await fixture.ResetAsync(factory.Services);

        await using var verificationConnection = new NpgsqlConnection(fixture.ConnectionString);
        await verificationConnection.OpenAsync(TestContext.Current.CancellationToken);
        await using var verificationCommand = verificationConnection.CreateCommand();
        verificationCommand.CommandText = "SELECT COUNT(*) FROM reset_probe;";
        var rowCount = (long)(await verificationCommand.ExecuteScalarAsync(
            TestContext.Current.CancellationToken))!;
        Assert.Equal(0, rowCount);
    }

    [Fact]
    public async Task ResetWithApplicationServicesRestoresRequiredRoles()
    {
        await using var fixture = new PostgreSqlFixture();
        await fixture.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(fixture.ConnectionString);
        using var client = factory.CreateClient();

        await fixture.ResetAsync(factory.Services);

        await using var scope = factory.Services.CreateAsyncScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        Assert.True(await roleManager.RoleExistsAsync("Customer"));
        Assert.True(await roleManager.RoleExistsAsync("Admin"));
    }
}
