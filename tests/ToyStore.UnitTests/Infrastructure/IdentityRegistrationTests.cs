using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class IdentityRegistrationTests
{
    [Fact]
    public void AddInfrastructureRegistersIdentityBehindApplicationBoundary()
    {
        var services = CreateServices();

        services.AddInfrastructure(CreateConfiguration());

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIdentityService)
                && descriptor.ImplementationType == typeof(IdentityService)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IIdentityInitializer)
                && descriptor.ImplementationType == typeof(IdentityInitializer)
                && descriptor.Lifetime == ServiceLifetime.Scoped);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IUserClaimsPrincipalFactory<ApplicationUser>)
                && descriptor.ImplementationType == typeof(ToyStoreClaimsPrincipalFactory));
    }

    [Fact]
    public void IdentityOptionsMatchTheApprovedEmailPasswordPolicy()
    {
        var services = CreateServices();
        services.AddInfrastructure(CreateConfiguration());

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<IdentityOptions>>().Value;

        Assert.False(options.SignIn.RequireConfirmedAccount);
        Assert.True(options.User.RequireUniqueEmail);
        Assert.Equal(IdentitySchemaVersions.Version2, options.Stores.SchemaVersion);
        Assert.Equal(8, options.Password.RequiredLength);
        Assert.True(options.Password.RequireDigit);
        Assert.True(options.Password.RequireLowercase);
        Assert.True(options.Password.RequireUppercase);
        Assert.False(options.Password.RequireNonAlphanumeric);
        Assert.Equal(5, options.Lockout.MaxFailedAccessAttempts);
        Assert.Equal(TimeSpan.FromMinutes(15), options.Lockout.DefaultLockoutTimeSpan);
    }

    [Fact]
    public void ThaiErrorDescriberDoesNotExposeEnglishIdentityMessages()
    {
        var describer = new ThaiIdentityErrorDescriber();

        Assert.Equal("อีเมลนี้ถูกใช้งานแล้ว", describer.DuplicateEmail("customer@example.com").Description);
        Assert.Equal("รหัสผ่านต้องมีตัวเลขอย่างน้อย 1 ตัว", describer.PasswordRequiresDigit().Description);
    }

    private static ServiceCollection CreateServices()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        return services;
    }

    private static IConfiguration CreateConfiguration() =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
            })
            .Build();
}
