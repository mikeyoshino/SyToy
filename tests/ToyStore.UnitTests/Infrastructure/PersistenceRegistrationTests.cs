using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ToyStore.Application;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.CreateBrand;
using ToyStore.Application.Catalog.Slugs;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.Infrastructure.Storage;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class PersistenceRegistrationTests
{
    [Fact]
    public void FullApplicationCompositionResolvesBrandMediaMutationHandler()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
                ["Storage:RootPath"] = storageRoot,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();

        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateBrandCommand, Result<BrandMutationResult>>>();

        Assert.IsType<CreateBrandHandler>(handler);
    }

    [Fact]
    public void FullApplicationCompositionResolvesCreateCharacterHandler()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplication();
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var handler = scope.ServiceProvider.GetRequiredService<
            IRequestHandler<CreateCharacterCommand, Result<CharacterOption>>>();

        Assert.IsType<CreateCharacterHandler>(handler);
    }

    [Fact]
    public void AddInfrastructureRegistersNpgsqlContextBehindApplicationBoundary()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();

        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        var concreteContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var applicationContext = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        var brandSessionFactory = scope.ServiceProvider
            .GetRequiredService<IBrandMutationSessionFactory>();
        var universeSessionFactory = scope.ServiceProvider
            .GetRequiredService<IUniverseMutationSessionFactory>();
        var characterSessionFactory = scope.ServiceProvider
            .GetRequiredService<ICharacterMutationSessionFactory>();
        var brandListReader = scope.ServiceProvider.GetRequiredService<IBrandListReader>();
        var universeListReader = scope.ServiceProvider.GetRequiredService<IUniverseListReader>();
        var characterSearchReader = scope.ServiceProvider.GetRequiredService<ICharacterSearchReader>();
        var mediaReferenceVerifier = scope.ServiceProvider
            .GetRequiredService<IMediaReferenceVerifier>();
        var persistenceFailureClassifier = scope.ServiceProvider
            .GetRequiredService<IPersistenceFailureClassifier>();

        Assert.Same(concreteContext, applicationContext);
        Assert.NotNull(contextFactory);
        Assert.NotNull(brandSessionFactory);
        Assert.NotNull(universeSessionFactory);
        Assert.NotNull(characterSessionFactory);
        Assert.NotNull(brandListReader);
        Assert.NotNull(universeListReader);
        Assert.NotNull(characterSearchReader);
        Assert.NotNull(mediaReferenceVerifier);
        Assert.Same(
            PostgresPersistenceFailureClassifier.Instance,
            persistenceFailureClassifier);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(ICatalogSlugAllocator));
        Assert.Equal(
            "Npgsql.EntityFrameworkCore.PostgreSQL",
            concreteContext.Database.ProviderName);
    }

    [Fact]
    public async Task CatalogOperationFactoriesNeverReuseScopedOrOperationContexts()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
            })
            .Build();
        var services = new ServiceCollection();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var contextFactory = scope.ServiceProvider
            .GetRequiredService<IDbContextFactory<ApplicationDbContext>>();
        await using var firstContext = await contextFactory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        await using var secondContext = await contextFactory.CreateDbContextAsync(
            TestContext.Current.CancellationToken);
        var brandFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
        var universeFactory = scope.ServiceProvider.GetRequiredService<IUniverseMutationSessionFactory>();
        await using var firstBrandSession = await brandFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        await using var secondBrandSession = await brandFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        await using var universeSession = await universeFactory.OpenAsync(
            TestContext.Current.CancellationToken);

        Assert.NotSame(scopedContext, firstContext);
        Assert.NotSame(firstContext, secondContext);
        Assert.NotSame(
            ContextOwnedBy(firstBrandSession),
            ContextOwnedBy(secondBrandSession));
        Assert.NotSame(
            ContextOwnedBy(firstBrandSession),
            ContextOwnedBy(universeSession));
        Assert.NotSame(scopedContext, ContextOwnedBy(firstBrandSession));
    }

    [Fact]
    public void AddInfrastructureRejectsMissingDatabaseConnectionStringWithoutLeakingConfiguration()
    {
        var configuration = new ConfigurationBuilder().Build();
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(
            () => services.AddInfrastructure(configuration));

        Assert.Equal(
            "Required connection string 'Database' is not configured.",
            exception.Message);
    }

    [Fact]
    public void AddInfrastructureRegistersOneSingletonBehindBothStorageBoundaries()
    {
        var storageRoot = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=localhost;Database=toystore_tests;Username=test;Password=test",
                ["Storage:RootPath"] = storageRoot,
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IHostEnvironment>(new TestHostEnvironment());
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();
        var storage = provider.GetRequiredService<IFileStorage>();
        var initializer = provider.GetRequiredService<IFileStorageInitializer>();

        Assert.Same(storage, initializer);
        Assert.IsType<LocalFileStorage>(storage);
        Assert.DoesNotContain(
            services,
            descriptor => descriptor.ServiceType == typeof(IHostedService) &&
                          descriptor.ImplementationType?.Namespace?.Contains(
                              "Storage",
                              StringComparison.Ordinal) == true);
    }

    private sealed class TestHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "ToyStore.UnitTests";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public Microsoft.Extensions.FileProviders.IFileProvider ContentRootFileProvider { get; set; } =
            new Microsoft.Extensions.FileProviders.NullFileProvider();
    }

    private static ApplicationDbContext ContextOwnedBy(object session)
    {
        var field = session.GetType()
            .GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .Single(candidate => candidate.FieldType == typeof(ApplicationDbContext));
        return Assert.IsType<ApplicationDbContext>(field.GetValue(session));
    }
}
