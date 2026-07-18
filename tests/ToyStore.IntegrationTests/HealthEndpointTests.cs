using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests;

public sealed class HealthEndpointTests
{
    [Fact]
    public void UnavailablePostgreSqlPreventsApplicationStartup()
    {
        const string connectionString =
            "Host=127.0.0.1;Port=1;Database=toystore_unavailable_test;Username=toystore;Password=not-a-secret;Timeout=1;Command Timeout=1;Pooling=false";
        using var factory = new ToyStoreWebApplicationFactory(connectionString);

        Assert.ThrowsAny<Exception>(() => factory.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }));
    }

    [Fact]
    public async Task LiveRemainsHealthyWhenPostgreSqlBecomesUnavailableAfterStartup()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var connectionString = new NpgsqlConnectionStringBuilder(postgreSql.ConnectionString)
        {
            Timeout = 1,
            CommandTimeout = 1,
            Pooling = false,
        }.ConnectionString;
        await using var factory = new ToyStoreWebApplicationFactory(connectionString);
        using var client = factory.CreateClient();

        using (var readyBeforeOutage = await client.GetAsync(
                   "/health/ready",
                   TestContext.Current.CancellationToken))
        {
            Assert.Equal(HttpStatusCode.OK, readyBeforeOutage.StatusCode);
        }

        await postgreSql.StopAsync();

        using var live = await client.GetAsync(
            "/health/live",
            TestContext.Current.CancellationToken);
        using var ready = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);
        using var combined = await client.GetAsync(
            "/health",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, live.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, ready.StatusCode);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, combined.StatusCode);
    }
}
