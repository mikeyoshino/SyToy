using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Infrastructure.Identity;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Identity;

public sealed class AccountEndpointTests
{
    [Theory]
    [InlineData("password1", "พิมพ์ใหญ่")]
    [InlineData("PASSWORD1", "พิมพ์เล็ก")]
    [InlineData("Passwordx", "ตัวเลข")]
    public async Task RegistrationShowsThaiFieldValidationForEveryPasswordClass(
        string password,
        string expectedMessage)
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });
        using var page = await client.GetAsync(
            "/Account/Register",
            TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        using var response = await PostFormAsync(
            client,
            "/Account/Register",
            html,
            new Dictionary<string, string>
            {
                ["_handler"] = "register",
                ["Input.Email"] = "invalid-password@example.com",
                ["Input.Password"] = password,
                ["Input.ConfirmPassword"] = password,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Contains(
            expectedMessage,
            WebUtility.HtmlDecode(responseHtml),
            StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("password1", "พิมพ์ใหญ่")]
    [InlineData("PASSWORD1", "พิมพ์เล็ก")]
    [InlineData("Passwordx", "ตัวเลข")]
    public async Task ChangePasswordShowsThaiFieldValidationForEveryPasswordClass(
        string password,
        string expectedMessage)
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
            var created = await bootstrapper.CreateFirstAdminAsync(
                "admin@example.com",
                "Temporary1",
                TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);
        }

        using var loginResponse = await LoginAsync(
            client,
            "admin@example.com",
            "Temporary1",
            rememberMe: false);
        using var page = await client.GetAsync(
            "/Account/Manage/ChangePassword",
            TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        using var response = await PostFormAsync(
            client,
            "/Account/Manage/ChangePassword",
            html,
            new Dictionary<string, string>
            {
                ["_handler"] = "change-password",
                ["Input.CurrentPassword"] = "Temporary1",
                ["Input.NewPassword"] = password,
                ["Input.ConfirmPassword"] = password,
            });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var responseHtml = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Contains(
            expectedMessage,
            WebUtility.HtmlDecode(responseHtml),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task RegisterLoginLogoutAndProtectedRouteUseThaiCookieFlow()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var registerPage = await client.GetAsync(
            "/Account/Register",
            TestContext.Current.CancellationToken);
        var registerHtml = await registerPage.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        var decodedRegisterHtml = WebUtility.HtmlDecode(registerHtml);
        Assert.Contains("สมัครสมาชิก", decodedRegisterHtml, StringComparison.Ordinal);
        Assert.Contains("ยืนยันรหัสผ่าน", decodedRegisterHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Email\"", registerHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Password\"", registerHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.ConfirmPassword\"", registerHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"__RequestVerificationToken\"", registerHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("Passkey", registerHtml, StringComparison.OrdinalIgnoreCase);

        using var registerResponse = await PostFormAsync(
            client,
            "/Account/Register",
            registerHtml,
            new Dictionary<string, string>
            {
                ["_handler"] = "register",
                ["Input.Email"] = "customer@example.com",
                ["Input.Password"] = "Password1",
                ["Input.ConfirmPassword"] = "Password1",
            });
        Assert.Equal(HttpStatusCode.Found, registerResponse.StatusCode);
        var registerLocation = Assert.IsType<Uri>(registerResponse.Headers.Location);
        Assert.Equal(
            "/",
            registerLocation.IsAbsoluteUri
                ? registerLocation.AbsolutePath
                : registerLocation.OriginalString);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var customer = await userManager.FindByEmailAsync("customer@example.com");
            Assert.NotNull(customer);
            Assert.True(await userManager.IsInRoleAsync(customer, RoleNames.Customer));
        }

        using var customerAdminResponse = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.OK, customerAdminResponse.StatusCode);

        await LogoutAsync(client);

        using var loginPage = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        var loginHtml = await loginPage.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Contains("เข้าสู่ระบบ", loginHtml, StringComparison.Ordinal);
        Assert.Contains("จดจำฉัน", loginHtml, StringComparison.Ordinal);
        Assert.Contains("ลืมรหัสผ่าน (ยังไม่เปิดใช้งาน)", loginHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Email\"", loginHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.Password\"", loginHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.RememberMe\"", loginHtml, StringComparison.Ordinal);

        using var loginResponse = await PostFormAsync(
            client,
            "/Account/Login",
            loginHtml,
            new Dictionary<string, string>
            {
                ["_handler"] = "login",
                ["Input.Email"] = "customer@example.com",
                ["Input.Password"] = "Password1",
                ["Input.RememberMe"] = "true",
            });
        Assert.Equal(HttpStatusCode.Found, loginResponse.StatusCode);
        var identityCookie = Assert.Single(
            loginResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith(
                ".AspNetCore.Identity.Application=",
                StringComparison.Ordinal));
        Assert.Contains("expires=", identityCookie, StringComparison.OrdinalIgnoreCase);

        await LogoutAsync(client);
        using var protectedResponse = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Found, protectedResponse.StatusCode);
        Assert.Contains(
            "/Account/Login",
            protectedResponse.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AccountStateChangesRejectMissingAndInvalidAntiforgeryTokens()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        using var registerPage = await client.GetAsync(
            "/Account/Register",
            TestContext.Current.CancellationToken);
        using var missingRegisterToken = await PostWithoutValidAntiforgeryAsync(
            client,
            "/Account/Register",
            token: null,
            values: new Dictionary<string, string>
            {
                ["_handler"] = "register",
                ["Input.Email"] = "antiforgery@example.com",
                ["Input.Password"] = "Password1",
                ["Input.ConfirmPassword"] = "Password1",
            });
        Assert.Equal(HttpStatusCode.BadRequest, missingRegisterToken.StatusCode);

        using var invalidLoginToken = await PostWithoutValidAntiforgeryAsync(
            client,
            "/Account/Login",
            token: "invalid-token",
            values: new Dictionary<string, string>
            {
                ["_handler"] = "login",
                ["Input.Email"] = "antiforgery@example.com",
                ["Input.Password"] = "Password1",
                ["Input.RememberMe"] = "false",
            });
        Assert.Equal(HttpStatusCode.BadRequest, invalidLoginToken.StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var identity = scope.ServiceProvider.GetRequiredService<IIdentityService>();
            var registration = await identity.RegisterCustomerAsync(
                "antiforgery@example.com",
                "Password1",
                TestContext.Current.CancellationToken);
            Assert.True(registration.IsSuccess);
        }

        using var loginResponse = await LoginAsync(
            client,
            "antiforgery@example.com",
            "Password1",
            rememberMe: false);
        using var missingLogoutToken = await PostWithoutValidAntiforgeryAsync(
            client,
            "/Account/Logout",
            token: null,
            values: new Dictionary<string, string> { ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.BadRequest, missingLogoutToken.StatusCode);
        using var invalidLogoutToken = await PostWithoutValidAntiforgeryAsync(
            client,
            "/Account/Logout",
            token: "invalid-token",
            values: new Dictionary<string, string> { ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidLogoutToken.StatusCode);
    }

    [Fact]
    public async Task FirstAdminMustChangePasswordBeforeUsingManagementPolicy()
    {
        await using var postgreSql = new PostgreSqlFixture();
        await postgreSql.InitializeAsync();
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = true,
        });

        string adminId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var bootstrapper = scope.ServiceProvider.GetRequiredService<IAdminBootstrapper>();
            var created = await bootstrapper.CreateFirstAdminAsync(
                "admin@example.com",
                "Temporary1",
                TestContext.Current.CancellationToken);
            Assert.True(created.IsSuccess);
            adminId = created.Value.UserId;
        }

        using var loginPage = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        var loginHtml = await loginPage.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var loginResponse = await PostFormAsync(
            client,
            "/Account/Login",
            loginHtml,
            new Dictionary<string, string>
            {
                ["_handler"] = "login",
                ["Input.Email"] = "admin@example.com",
                ["Input.Password"] = "Temporary1",
                ["Input.RememberMe"] = "false",
            });

        Assert.Equal(HttpStatusCode.Found, loginResponse.StatusCode);
        Assert.Contains(
            "/Account/Manage/ChangePassword",
            loginResponse.Headers.Location?.OriginalString,
            StringComparison.OrdinalIgnoreCase);
        using var deniedResponse = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        Assert.NotEqual(HttpStatusCode.OK, deniedResponse.StatusCode);

        using var changePage = await client.GetAsync(
            "/Account/Manage/ChangePassword",
            TestContext.Current.CancellationToken);
        var changeHtml = await changePage.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Contains("name=\"Input.CurrentPassword\"", changeHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.NewPassword\"", changeHtml, StringComparison.Ordinal);
        Assert.Contains("name=\"Input.ConfirmPassword\"", changeHtml, StringComparison.Ordinal);
        using var changeResponse = await PostFormAsync(
            client,
            "/Account/Manage/ChangePassword",
            changeHtml,
            new Dictionary<string, string>
            {
                ["_handler"] = "change-password",
                ["Input.CurrentPassword"] = "Temporary1",
                ["Input.NewPassword"] = "ChangedPassword1",
                ["Input.ConfirmPassword"] = "ChangedPassword1",
            });
        Assert.Equal(HttpStatusCode.Found, changeResponse.StatusCode);

        using var allowedResponse = await client.GetAsync(
            "/admin",
            TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, allowedResponse.StatusCode);
        var allowedHtml = await allowedResponse.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        Assert.Contains("ภาพรวมร้าน", allowedHtml, StringComparison.Ordinal);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var userManager = verificationScope.ServiceProvider
            .GetRequiredService<UserManager<ApplicationUser>>();
        var admin = await userManager.FindByIdAsync(adminId);
        Assert.NotNull(admin);
        Assert.False(admin.MustChangePassword);
    }

    private static async Task LogoutAsync(HttpClient client)
    {
        using var homePage = await client.GetAsync("/", TestContext.Current.CancellationToken);
        var homeHtml = await homePage.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var response = await PostFormAsync(
            client,
            "/Account/Logout",
            homeHtml,
            new Dictionary<string, string> { ["returnUrl"] = "/" });
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
    }

    private static async Task<HttpResponseMessage> LoginAsync(
        HttpClient client,
        string email,
        string password,
        bool rememberMe)
    {
        using var page = await client.GetAsync(
            "/Account/Login",
            TestContext.Current.CancellationToken);
        var html = await page.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var response = await PostFormAsync(
            client,
            "/Account/Login",
            html,
            new Dictionary<string, string>
            {
                ["_handler"] = "login",
                ["Input.Email"] = email,
                ["Input.Password"] = password,
                ["Input.RememberMe"] = rememberMe.ToString(),
            });
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        return response;
    }

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string path,
        string html,
        Dictionary<string, string> values)
    {
        values["__RequestVerificationToken"] = ExtractAntiforgeryToken(html);
        using var content = new FormUrlEncodedContent(values);
        return await client.PostAsync(path, content, TestContext.Current.CancellationToken);
    }

    private static async Task<HttpResponseMessage> PostWithoutValidAntiforgeryAsync(
        HttpClient client,
        string path,
        string? token,
        Dictionary<string, string> values)
    {
        if (token is not null)
        {
            values["__RequestVerificationToken"] = token;
        }

        using var content = new FormUrlEncodedContent(values);
        return await client.PostAsync(
            path,
            content,
            TestContext.Current.CancellationToken);
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
}
