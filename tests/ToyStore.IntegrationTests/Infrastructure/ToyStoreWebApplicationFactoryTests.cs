using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.IntegrationTests.Infrastructure;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ToyStoreWebApplicationFactoryTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task UsesThrowawayPostgreSqlAndStartsApplication()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(
            "/health/ready",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(PostgreSqlFixture.IsSafeTestDatabase(dbContext.Database.GetConnectionString()!));
    }
}
