using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Infrastructure.Identity;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Admin;

public sealed class AdminAuthorizationTests
{
    private static readonly string[] ProtectedPaths =
    [
        "/admin",
        "/admin/products",
        "/admin/brands",
        "/admin/universes",
        "/admin/inventory",
        "/admin/orders?type=pre-order",
        "/admin/notifications",
        "/admin/reports",
        "/admin/settings",
    ];

    [Fact]
    public async Task DirectSsrAdminRoutesUseDeterministicCookieRedirectsAndValidAdminShell()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        await CreateUserAsync(factory, "customer-admin-route@example.com", RoleNames.Customer, false);
        await CreateUserAsync(factory, "forced-customer-admin-route@example.com", RoleNames.Customer, true);
        await CreateUserAsync(factory, "forced-admin-route@example.com", RoleNames.Admin, true);
        await CreateUserAsync(factory, "valid-admin-route@example.com", RoleNames.Admin, false);

        using var anonymous = CreateClient(factory);
        using var customer = CreateClient(factory);
        using var forcedCustomer = CreateClient(factory);
        using var forcedAdmin = CreateClient(factory);
        using var validAdmin = CreateClient(factory);
        await LoginAsync(customer, "customer-admin-route@example.com");
        await LoginAsync(forcedCustomer, "forced-customer-admin-route@example.com");
        await LoginAsync(forcedAdmin, "forced-admin-route@example.com");
        await LoginAsync(validAdmin, "valid-admin-route@example.com");

        foreach (var path in ProtectedPaths)
        {
            using var anonymousResponse = await anonymous.GetAsync(
                path,
                TestContext.Current.CancellationToken);
            AssertRedirect(
                anonymousResponse,
                $"/Account/Login?ReturnUrl={Uri.EscapeDataString(path)}");

            using var customerResponse = await customer.GetAsync(
                path,
                TestContext.Current.CancellationToken);
            AssertRedirect(customerResponse, "/Account/AccessDenied");

            using var forcedCustomerResponse = await forcedCustomer.GetAsync(
                path,
                TestContext.Current.CancellationToken);
            AssertRedirect(forcedCustomerResponse, "/Account/AccessDenied");

            using var forcedResponse = await forcedAdmin.GetAsync(
                path,
                TestContext.Current.CancellationToken);
            AssertRedirect(forcedResponse, "/Account/Manage/ChangePassword");

            using var validResponse = await validAdmin.GetAsync(
                path,
                TestContext.Current.CancellationToken);
            Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
            var html = WebUtility.HtmlDecode(await validResponse.Content.ReadAsStringAsync(
                TestContext.Current.CancellationToken));
            Assert.Contains("ข้ามไปยังเนื้อหาหลัก", html, StringComparison.Ordinal);
            Assert.Contains("เมนูผู้ดูแลระบบ", html, StringComparison.Ordinal);
            Assert.Contains("<h1", html, StringComparison.Ordinal);
            if (path == "/admin/brands")
            {
                Assert.Contains("เพิ่มแบรนด์", html, StringComparison.Ordinal);
                Assert.Contains("ค้นหาและกรองแบรนด์", html, StringComparison.Ordinal);
                Assert.Contains("ยังไม่มีแบรนด์", html, StringComparison.Ordinal);
                Assert.DoesNotContain("การจัดการแบรนด์จะพร้อมใช้งานในระยะถัดไป", html, StringComparison.Ordinal);
            }
            else if (path == "/admin/universes")
            {
                Assert.Contains("เพิ่มจักรวาล", html, StringComparison.Ordinal);
                Assert.Contains("ค้นหาและกรองจักรวาล", html, StringComparison.Ordinal);
                Assert.Contains("ต้องเพิ่มโลโก้", html, StringComparison.Ordinal);
                Assert.Contains("ตัวละคร 0 รายการ", html, StringComparison.Ordinal);
                Assert.DoesNotContain(
                    "การจัดการจักรวาลจะพร้อมใช้งานในระยะถัดไป",
                    html,
                    StringComparison.Ordinal);
            }
        }

        using var malformedUniversePage = await validAdmin.GetAsync(
            "/admin/universes?page=abc",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, malformedUniversePage.StatusCode);
        var canonicalUniverseLocation = Assert.IsType<Uri>(
            malformedUniversePage.Headers.Location);
        Assert.Equal(
            "/admin/universes",
            canonicalUniverseLocation.IsAbsoluteUri
                ? canonicalUniverseLocation.PathAndQuery
                : canonicalUniverseLocation.OriginalString);

        using var loginDestination = await anonymous.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, loginDestination.StatusCode);
        using var deniedDestination = await customer.GetAsync(
            "/Account/AccessDenied",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, deniedDestination.StatusCode);
        using var passwordDestination = await forcedAdmin.GetAsync(
            "/Account/Manage/ChangePassword",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, passwordDestination.StatusCode);
    }

    [Fact]
    public async Task RealAdminLogoutFormIsAntiforgeryProtectedSignsOutAndChallengesAgain()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        await CreateUserAsync(factory, "logout-admin@example.com", RoleNames.Admin, false);
        using var client = CreateClient(factory);
        await LoginAsync(client, "logout-admin@example.com");

        using var adminPage = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        var html = await adminPage.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var token = ExtractAntiforgeryToken(html);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = "/admin",
        });

        using var logout = await client.PostAsync(
            "/Account/Logout",
            content,
            TestContext.Current.CancellationToken);

        AssertRedirect(logout, "/admin");
        using var challenged = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        AssertRedirect(
            challenged,
            "/Account/Login?ReturnUrl=%2Fadmin");
    }

    [Fact]
    public async Task AuthenticatedMalformedBrandPageQueryRedirectsSafelyToCanonicalFirstPage()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        await CreateUserAsync(factory, "malformed-brand-page@example.com", RoleNames.Admin, false);
        using var client = CreateClient(factory);
        await LoginAsync(client, "malformed-brand-page@example.com");

        using var malformed = await client.GetAsync(
            "/admin/brands?page=abc",
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Found, malformed.StatusCode);
        var canonicalLocation = Assert.IsType<Uri>(malformed.Headers.Location);
        Assert.Equal(
            "/admin/brands",
            canonicalLocation.IsAbsoluteUri
                ? canonicalLocation.PathAndQuery
                : canonicalLocation.OriginalString);
        using var canonical = await client.GetAsync(
            canonicalLocation,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, canonical.StatusCode);
        var html = WebUtility.HtmlDecode(await canonical.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));
        Assert.Contains("ยังไม่มีแบรนด์", html, StringComparison.Ordinal);
    }

    private static HttpClient CreateClient(ToyStoreWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

    private static async Task CreateUserAsync(
        ToyStoreWebApplicationFactory factory,
        string email,
        string role,
        bool mustChangePassword)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            MustChangePassword = mustChangePassword,
        };
        var created = await manager.CreateAsync(user, "Password1");
        Assert.True(created.Succeeded, string.Join(", ", created.Errors.Select(error => error.Description)));
        var roleResult = await manager.AddToRoleAsync(user, role);
        Assert.True(roleResult.Succeeded);
    }

    private static async Task LoginAsync(HttpClient client, string email)
    {
        using var page = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = ExtractAntiforgeryToken(html),
            ["_handler"] = "login",
            ["Input.Email"] = email,
            ["Input.Password"] = "Password1",
            ["Input.RememberMe"] = "false",
        });
        using var response = await client.PostAsync(
            "/Account/Login",
            content,
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static string ExtractAntiforgeryToken(string html)
    {
        var match = Regex.Match(
            html,
            "name=\"__RequestVerificationToken\" value=\"([^\"]+)\"",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(1));
        Assert.True(match.Success, "Expected an antiforgery token in the rendered form.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static void AssertRedirect(HttpResponseMessage response, string expectedLocation)
    {
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.False(location.IsAbsoluteUri);
        Assert.Equal(expectedLocation, location.OriginalString);
    }
}
