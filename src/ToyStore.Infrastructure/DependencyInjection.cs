using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using ToyStore.Application.Brands;
using ToyStore.Application.Addresses;
using ToyStore.Application.Cart;
using ToyStore.Application.Checkout;
using ToyStore.Application.Characters;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Application.Orders;
using ToyStore.Application.PreOrders;
using ToyStore.Application.Products;
using ToyStore.Application.Products.ManageProducts;
using ToyStore.Application.Storefront.Catalog;
using ToyStore.Application.Universes;
using ToyStore.Infrastructure.Identity;
using ToyStore.Infrastructure.Addresses;
using ToyStore.Infrastructure.Persistence;
using ToyStore.Infrastructure.Payments;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString("Database");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Required connection string 'Database' is not configured.");
        }

        services.AddDbContextFactory<ApplicationDbContext>(options =>
            options.UseNpgsql(
                connectionString,
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName)));
        services.AddScoped<IApplicationDbContext>(provider =>
            provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IPersistenceFailureClassifier>(
            PostgresPersistenceFailureClassifier.Instance);
        services.AddSingleton<ThaiAddressCatalog>(_ => ThaiAddressCatalog.Load());
        services.AddSingleton<IThaiAddressCatalog>(provider =>
            provider.GetRequiredService<ThaiAddressCatalog>());
        services.AddSingleton<IBrandListReader, BrandListReader>();
        services.AddSingleton<IUniverseListReader, UniverseListReader>();
        services.AddSingleton<ICharacterSearchReader, CharacterSearchReader>();
        services.AddSingleton<IBrandMutationSessionFactory, BrandMutationSessionFactory>();
        services.AddSingleton<IUniverseMutationSessionFactory, UniverseMutationSessionFactory>();
        services.AddSingleton<ICharacterMutationSessionFactory, CharacterMutationSessionFactory>();
        services.AddSingleton<IInventoryMutationSessionFactory, InventoryMutationSessionFactory>();
        services.AddSingleton<IPreOrderCapacityMutationSessionFactory, PreOrderCapacityMutationSessionFactory>();
        services.AddSingleton<IPreOrderCheckoutEligibilityReader, PreOrderCheckoutEligibilityReader>();
        services.AddSingleton<IPreOrderCheckoutStore, PreOrderCheckoutStore>();
        services.AddSingleton<IInStockCheckoutStore, InStockCheckoutStore>();
        services.AddSingleton<ICheckoutCustomerReader, CheckoutCustomerReader>();
        services.AddOptions<StripePaymentOptions>()
            .Bind(configuration.GetSection(StripePaymentOptions.SectionName));
        services.AddSingleton<IPaymentGateway, StripePaymentGateway>();
        services.AddSingleton<IProductMutationSessionFactory, ProductMutationSessionFactory>();
        services.AddSingleton<IProductManagementReader, ProductManagementReader>();
        services.AddSingleton<IStorefrontCatalogReader, StorefrontCatalogReader>();
        services.AddSingleton<ICartMutationSessionFactory, CartMutationSessionFactory>();
        services.AddSingleton<ICartReader, CartReader>();
        services.AddSingleton<ICustomerOrderReader, CustomerOrderReader>();
        services.AddSingleton<IInventoryReadStore, InventoryReadStore>();
        services.AddSingleton<IMediaReferenceVerifier, MediaReferenceVerifier>();
        services.AddSingleton<IMediaCleanupRegistry, MediaCleanupRegistry>();
        services.AddOptions<LocalFileStorageOptions>()
            .Bind(configuration.GetSection(LocalFileStorageOptions.SectionName))
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<LocalFileStorageOptions>, LocalFileStorageOptionsValidator>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton<LocalFileStorage>();
        services.AddSingleton<IFileStorage>(provider =>
            provider.GetRequiredService<LocalFileStorage>());
        services.AddSingleton<IFileStorageInitializer>(provider =>
            provider.GetRequiredService<LocalFileStorage>());
        services.AddIdentityCore<ApplicationUser>(options =>
            {
                options.SignIn.RequireConfirmedAccount = false;
                options.User.RequireUniqueEmail = true;
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version2;
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = false;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddSignInManager()
            .AddDefaultTokenProviders()
            .AddErrorDescriber<ThaiIdentityErrorDescriber>()
            .AddClaimsPrincipalFactory<ToyStoreClaimsPrincipalFactory>();
        services.AddScoped<IIdentityService, IdentityService>();
        services.AddScoped<IUserSessionValidator, UserSessionValidator>();
        services.AddScoped<IAdminBootstrapper, AdminBootstrapper>();
        services.AddScoped<IIdentityInitializer, IdentityInitializer>();
        services.AddHealthChecks()
            .AddDbContextCheck<ApplicationDbContext>(
                "postgresql",
                HealthStatus.Unhealthy,
                ["ready"]);

        return services;
    }
}
