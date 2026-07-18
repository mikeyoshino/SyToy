using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ForwardedHeadersTests(PostgreSqlFixture postgreSql)
{
    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("::1")]
    public async Task TrustedLoopbackProxyCanForwardHttpsScheme(string remoteIpAddress)
    {
        await using var factory = new ProxyWebApplicationFactory(
            postgreSql.ConnectionString,
            IPAddress.Parse(remoteIpAddress));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = CreateForwardedHttpsRequest();

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UntrustedProxyCannotForwardHttpsScheme()
    {
        await using var factory = new ProxyWebApplicationFactory(
            postgreSql.ConnectionString,
            IPAddress.Parse("192.0.2.10"));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = CreateForwardedHttpsRequest();

        using var response = await client.SendAsync(
            request,
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.TemporaryRedirect, response.StatusCode);
        Assert.Equal("https", response.Headers.Location?.Scheme);
    }

    private static HttpRequestMessage CreateForwardedHttpsRequest()
    {
        var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add("X-Forwarded-Proto", "https");
        request.Headers.Add("X-Forwarded-For", "203.0.113.20");
        return request;
    }

    private sealed class ProxyWebApplicationFactory(
        string connectionString,
        IPAddress remoteIpAddress) : ToyStoreWebApplicationFactory(connectionString)
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.Configure<HttpsRedirectionOptions>(options => options.HttpsPort = 443);
                services.AddSingleton<IStartupFilter>(new RemoteIpStartupFilter(remoteIpAddress));
            });
        }
    }

    private sealed class RemoteIpStartupFilter(IPAddress remoteIpAddress) : IStartupFilter
    {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
            application =>
            {
                application.Use((context, nextMiddleware) =>
                {
                    context.Connection.RemoteIpAddress = remoteIpAddress;
                    return nextMiddleware(context);
                });
                next(application);
            };
    }
}
