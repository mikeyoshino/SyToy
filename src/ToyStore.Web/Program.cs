using System.Net;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Serilog;
using ToyStore.Application;
using ToyStore.Application.Addresses;
using ToyStore.Infrastructure;
using ToyStore.Web.Components;
using ToyStore.Web.Components.Feedback;
using ToyStore.Web.Components.Account;
using ToyStore.Web.Components.Admin.Primitives;
using ToyStore.Web.Components.Cart;
using ToyStore.Web.Diagnostics;
using ToyStore.Web.Identity;
using ToyStore.Web.Media;
using ToyStore.Web.Payments;
using ToyStore.Web.Startup;

var bootstrapAdminRequested = AdminBootstrapCommand.IsRequested(args);
var hostArguments = args
    .Where(argument => !string.Equals(
        argument,
        "--bootstrap-admin",
        StringComparison.Ordinal))
    .ToArray();
var builder = WebApplication.CreateBuilder(hostArguments);

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services));

// Add services to the container.
builder.Services.AddApplication();
builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.ForwardLimit = 1;
    options.KnownProxies.Clear();
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Add(IPAddress.Loopback);
    options.KnownProxies.Add(IPAddress.IPv6Loopback);

    var configuredProxy = builder.Configuration["ReverseProxy:KnownProxy"];
    if (!string.IsNullOrWhiteSpace(configuredProxy))
    {
        if (!IPAddress.TryParse(configuredProxy, out var knownProxy))
        {
            throw new InvalidOperationException(
                "ReverseProxy:KnownProxy must be a valid IP address.");
        }

        options.KnownProxies.Add(knownProxy);
    }
});
builder.Services.AddHealthChecks()
    .AddCheck(
        "self",
        () => HealthCheckResult.Healthy(),
        ["live"]);
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddCascadingAuthenticationState();
builder.Services.AddScoped<AdminRequestExecutor>();
builder.Services.AddScoped<AdminCharacterAutocompleteAdapter>();
builder.Services.AddScoped<CartDrawerCoordinator>();
builder.Services.AddScoped<IStoreToastService, StoreToastService>();
builder.Services.AddScoped<AnonymousCartBrowserStore>();
builder.Services.AddScoped<IdentityRedirectManager>();
builder.Services.AddScoped<AuthenticationStateProvider, IdentityRevalidatingAuthenticationStateProvider>();

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddToyStoreWebIdentity(builder.Configuration, builder.Environment);

var app = builder.Build();

_ = app.Services.GetRequiredService<IThaiAddressCatalog>();
await app.ApplyMigrationsAndSeedIdentityAsync();
await app.InitializeFileStorageAsync();

if (bootstrapAdminRequested)
{
    await using var scope = app.Services.CreateAsyncScope();
    var command = scope.ServiceProvider.GetRequiredService<AdminBootstrapCommand>();
    Environment.ExitCode = await command.ExecuteAsync(CancellationToken.None);
    return;
}

// Configure the HTTP request pipeline.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseSerilogRequestLogging(options =>
    options.GetLevel = RequestLogLevelSelector.Select);
app.UseExceptionHandler();
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapToyStoreMedia();
app.MapStripeWebhook();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Add additional endpoints required by the Identity /Account Razor components.
app.MapAdditionalIdentityEndpoints();
app.MapHealthChecks("/health");
app.MapHealthChecks(
    "/health/live",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("live"),
    });
app.MapHealthChecks(
    "/health/ready",
    new HealthCheckOptions
    {
        Predicate = registration => registration.Tags.Contains("ready"),
    });

app.Run();

public partial class Program;
