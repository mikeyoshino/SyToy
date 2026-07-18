using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Web.Components.Account;

namespace ToyStore.Web.Identity;

public static class IdentityWebConfiguration
{
    public static IServiceCollection AddToyStoreWebIdentity(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var keysPath = configuration["DataProtection:KeysPath"];
        if (string.IsNullOrWhiteSpace(keysPath))
        {
            if (!environment.IsDevelopment())
            {
                throw new InvalidOperationException(
                    "DataProtection:KeysPath is required outside Development.");
            }

            keysPath = Path.Combine(environment.ContentRootPath, ".data", "keys");
        }

        Directory.CreateDirectory(keysPath);
        services.AddDataProtection()
            .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
            .SetApplicationName(configuration["DataProtection:ApplicationName"] ?? "ToyStore");

        services.AddHttpContextAccessor();
        services.AddScoped<IUserContext, HttpUserContext>();
        services.AddScoped<ICurrentUserAuthorization, CurrentUserAuthorization>();
        services.AddScoped<AdminBootstrapCommand>();
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddIdentityCookies();
        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.HttpOnly = true;
            options.Cookie.SameSite = SameSiteMode.Lax;
            options.Cookie.SecurePolicy = environment.IsDevelopment()
                || environment.IsEnvironment("Testing")
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;
            options.ExpireTimeSpan = TimeSpan.FromDays(14);
            options.SlidingExpiration = true;
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.Events.OnRedirectToLogin = context =>
            {
                var returnUrl = RequestLocalUrl(context.Request);
                context.Response.Redirect(
                    $"{options.LoginPath}?ReturnUrl={Uri.EscapeDataString(returnUrl)}");
                return Task.CompletedTask;
            };
            options.Events.OnRedirectToAccessDenied = context =>
            {
                var user = context.HttpContext.User;
                var destination = user.IsInRole(RoleNames.Admin)
                    && user.HasClaim(
                        IdentityClaimNames.MustChangePassword,
                        bool.TrueString)
                    ? "/Account/Manage/ChangePassword"
                    : options.AccessDeniedPath.Value!;
                context.Response.Redirect(destination);
                return Task.CompletedTask;
            };
        });

        var authorization = services.AddAuthorizationBuilder();
        authorization.AddPolicy(PolicyNames.CanUseCustomerCart, policy =>
            policy.RequireRole(RoleNames.Customer));
        authorization.AddPolicy(PolicyNames.CanViewCustomerOrders, policy =>
            policy.RequireRole(RoleNames.Customer));
        foreach (var policyName in ManagementPolicies())
        {
            authorization.AddPolicy(policyName, policy =>
            {
                policy.RequireRole(RoleNames.Admin);
                policy.RequireAssertion(context =>
                    !context.User.HasClaim(
                        IdentityClaimNames.MustChangePassword,
                        bool.TrueString));
            });
        }

        return services;
    }

    private static string[] ManagementPolicies() =>
    [
        PolicyNames.CanAccessAdmin,
        PolicyNames.CanManageProducts,
        PolicyNames.CanManageOrders,
        PolicyNames.CanVerifyPayments,
        PolicyNames.CanManageUsers,
    ];

    private static string RequestLocalUrl(HttpRequest request) =>
        LocalReturnUrl.Normalize($"{request.PathBase}{request.Path}{request.QueryString}");
}
