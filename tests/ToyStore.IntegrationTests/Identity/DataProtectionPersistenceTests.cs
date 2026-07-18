using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Interfaces;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Identity;

public sealed class DataProtectionPersistenceTests
{
    [Fact]
    public async Task PersistentCookieSurvivesRestartOnlyWithSharedKeysAndApplicationName()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        var keysPath = Path.Combine(
            Path.GetTempPath(),
            "toystore-data-protection-tests",
            Guid.NewGuid().ToString("N"));

        try
        {
            string cookie;
            await using (var firstFactory = new ToyStoreWebApplicationFactory(
                postgreSql.ConnectionString,
                keysPath,
                "ToyStore-Restart-Test"))
            {
                using var client = firstFactory.CreateClient(new WebApplicationFactoryClientOptions
                {
                    AllowAutoRedirect = false,
                    HandleCookies = true,
                });
                await using (var scope = firstFactory.Services.CreateAsyncScope())
                {
                    var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
                    var registration = await identity.RegisterCustomerAsync(
                        "restart@example.com",
                        "Password1",
                        TestContext.Current.CancellationToken);
                    Assert.True(registration.IsSuccess);
                }

                using var loginPage = await client.GetAsync(
                    "/Account/Login",
                    TestContext.Current.CancellationToken);
                var html = await loginPage.Content.ReadAsStringAsync(
                    TestContext.Current.CancellationToken);
                using var loginResponse = await PostLoginAsync(client, html);
                cookie = Assert.Single(
                        loginResponse.Headers.GetValues("Set-Cookie"),
                        value => value.StartsWith(
                            ".AspNetCore.Identity.Application=",
                            StringComparison.Ordinal))
                    .Split(';', 2)[0];
                Assert.Contains("expires=", loginResponse.Headers.GetValues("Set-Cookie").Single(
                    value => value.StartsWith(
                        ".AspNetCore.Identity.Application=",
                        StringComparison.Ordinal)), StringComparison.OrdinalIgnoreCase);
            }

            await using (var restartedFactory = new ToyStoreWebApplicationFactory(
                postgreSql.ConnectionString,
                keysPath,
                "ToyStore-Restart-Test"))
            {
                using var client = CreateClientWithCookie(restartedFactory, cookie);
                using var response = await client.GetAsync(
                    "/Account/Manage/ChangePassword",
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            }

            await using (var isolatedFactory = new ToyStoreWebApplicationFactory(
                postgreSql.ConnectionString,
                keysPath,
                "ToyStore-Different-Application"))
            {
                using var client = CreateClientWithCookie(isolatedFactory, cookie);
                using var response = await client.GetAsync(
                    "/Account/Manage/ChangePassword",
                    TestContext.Current.CancellationToken);
                Assert.Equal(HttpStatusCode.Found, response.StatusCode);
                Assert.Contains(
                    "/Account/Login",
                    response.Headers.Location?.OriginalString,
                    StringComparison.OrdinalIgnoreCase);
            }
        }
        finally
        {
            if (Directory.Exists(keysPath))
            {
                Directory.Delete(keysPath, recursive: true);
            }
        }
    }

    private static HttpClient CreateClientWithCookie(
        ToyStoreWebApplicationFactory factory,
        string cookie)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false,
        });
        client.DefaultRequestHeaders.Add("Cookie", cookie);
        return client;
    }

    private static async Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        Assert.True(match.Success);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value),
            ["_handler"] = "login",
            ["Input.Email"] = "restart@example.com",
            ["Input.Password"] = "Password1",
            ["Input.RememberMe"] = "true",
        });
        var response = await client.PostAsync(
            "/Account/Login",
            content,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return response;
    }
}
