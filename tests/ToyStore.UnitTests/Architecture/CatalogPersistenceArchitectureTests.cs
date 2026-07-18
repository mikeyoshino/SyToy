using System.Reflection;
using ToyStore.Application.Brands;
using ToyStore.Application.Catalog.Slugs;
using ToyStore.Application.Characters;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Application.Universes;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Architecture;

public sealed class CatalogPersistenceArchitectureTests
{
    private static readonly string[] ForbiddenTransactionMethods =
        ["BeginAsync", "SaveChangesAsync", "CommitAsync", "RollbackAsync"];
    private static readonly string[] SessionSourceFiles =
        ["BrandMutationSession.cs", "UniverseMutationSession.cs", "CharacterMutationSession.cs"];

    [Fact]
    public void DomainPersistenceConstructorsAreNeverPublic()
    {
        Type[] mappedTypes =
        [
            typeof(Product), typeof(ProductImage), typeof(ProductCharacter),
            typeof(InStockOffer), typeof(PreOrderOffer), typeof(Money), typeof(EstimatedArrival),
            typeof(ProductCategory), typeof(Brand), typeof(Universe), typeof(Character),
            typeof(CatalogMediaReference),
        ];

        Assert.All(mappedTypes, type => Assert.DoesNotContain(
            type.GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => constructor.GetParameters().Length == 0));
    }

    [Fact]
    public void ApplicationPersistenceContractsRemainProviderAgnosticAndSpecialized()
    {
        var contracts = new[]
        {
            typeof(IApplicationDbContext), typeof(ICatalogSlugAllocator),
            typeof(ICharacterSearchReader), typeof(ICharacterMutationSessionFactory),
            typeof(ICharacterMutationSession),
            typeof(IBrandListReader), typeof(IBrandMutationSessionFactory),
            typeof(IBrandMutationSession), typeof(IUniverseListReader),
            typeof(IUniverseMutationSessionFactory), typeof(IUniverseMutationSession),
            typeof(ICatalogMutationSession), typeof(IMediaReferenceVerifier),
            typeof(IMediaCleanupRegistry), typeof(IPersistenceFailureClassifier),
            typeof(IPersistenceFailureResultRequest<>),
        };
        Assert.All(contracts.SelectMany(type => type.GetMethods()), method =>
        {
            var signature = method + string.Join(',', method.GetParameters().Select(value => value.ParameterType.FullName));
            Assert.DoesNotContain("EntityFrameworkCore", signature, StringComparison.Ordinal);
            Assert.DoesNotContain("Npgsql", signature, StringComparison.Ordinal);
            Assert.DoesNotContain("IQueryable", signature, StringComparison.Ordinal);
            Assert.DoesNotContain("DbSet", signature, StringComparison.Ordinal);
        });

        Assert.Equal(
            ["AllocateBrandAsync", "AllocateProductAsync", "AllocateUniverseAsync"],
            typeof(ICatalogSlugAllocator).GetMethods().Select(value => value.Name).Order());
        Assert.DoesNotContain(
            typeof(IApplicationDbContext).Assembly.GetTypes(),
            type => type.Name.Contains("Repository", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MutationSessionsExposeOneOperationOwnedTransactionBoundary()
    {
        Assert.Contains(
            typeof(ICatalogMutationSession).GetMethods(),
            method => method.Name == nameof(ICatalogMutationSession.ExecuteOnceAsync));

        Assert.All(
            new[]
            {
                typeof(IBrandMutationSession),
                typeof(IUniverseMutationSession),
                typeof(ICharacterMutationSession),
            },
            contract => Assert.DoesNotContain(
                contract.GetMethods(),
                method => ForbiddenTransactionMethods.Contains(
                    method.Name,
                    StringComparer.Ordinal)));

        var repositoryRoot = FindRepositoryRoot();
        var sources = SessionSourceFiles
            .Select(file => File.ReadAllText(Path.Combine(
                repositoryRoot,
                "src",
                "ToyStore.Infrastructure",
                "Persistence",
                file)))
            .ToArray();

        Assert.All(sources, source =>
        {
            Assert.DoesNotContain("CreateExecutionStrategy", source, StringComparison.Ordinal);
            Assert.Contains("Interlocked.Exchange", source, StringComparison.Ordinal);
            Assert.Contains("CancellationToken.None", source, StringComparison.Ordinal);
            Assert.Contains("ChangeTracker.Clear", source, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void CharacterPortsCannotExposeCircuitPersistenceAndRazorCannotOwnCharacterDataAccess()
    {
        var characterContracts = new[]
        {
            typeof(ICharacterSearchReader),
            typeof(ICharacterMutationSessionFactory),
            typeof(ICharacterMutationSession),
        };
        var signatures = string.Join(
            '\n',
            characterContracts
                .SelectMany(type => type.GetMethods())
                .Select(method => method + string.Join(
                    ',',
                    method.GetParameters().Select(parameter => parameter.ParameterType.FullName))));

        Assert.DoesNotContain(nameof(IApplicationDbContext), signatures, StringComparison.Ordinal);

        var repositoryRoot = FindRepositoryRoot();
        var razorSources = Directory
            .EnumerateFiles(
                Path.Combine(repositoryRoot, "src", "ToyStore.Web"),
                "*.razor",
                SearchOption.AllDirectories)
            .Select(File.ReadAllText);
        Assert.All(razorSources, source =>
            Assert.DoesNotContain("ApplicationDbContext", source, StringComparison.Ordinal));
    }

    [Fact]
    public void CleanupKeysCanOnlyComeFromTrustedMediaContracts()
    {
        Assert.Empty(typeof(TrustedMediaStorageKey).GetConstructors());
        Assert.Equal(
            [nameof(TrustedMediaStorageKey.From), nameof(TrustedMediaStorageKey.From)],
            typeof(TrustedMediaStorageKey)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .Where(method => method.DeclaringType == typeof(TrustedMediaStorageKey)
                    && method.Name == nameof(TrustedMediaStorageKey.From))
                .Select(method => method.Name)
                .Order());

        Assert.DoesNotContain(
            typeof(TrustedMediaStorageKey).GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static),
            method => method.DeclaringType == typeof(TrustedMediaStorageKey)
                && method.GetParameters().Any(parameter => parameter.ParameterType == typeof(string)));

        Assert.All(
            typeof(ProductMediaSnapshot).GetMethods(BindingFlags.Public | BindingFlags.Static),
            method => Assert.All(method.GetParameters(), parameter =>
                Assert.DoesNotContain(
                    typeof(string),
                    FlattenGenericArguments(parameter.ParameterType))));

        var coordinatorSignatures = typeof(ProductMediaMutationCoordinator)
            .GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Select(method => method.ToString())
            .ToArray();
        Assert.All(coordinatorSignatures, signature =>
            Assert.DoesNotContain("IReadOnlyCollection`1[System.String]", signature, StringComparison.Ordinal));
    }

    [Fact]
    public void CatalogModelHasNoVariantSkuOrCategoryHierarchyType()
    {
        var names = typeof(Product).Assembly.GetTypes().Select(type => type.Name).ToArray();
        Assert.DoesNotContain(names, name => name.Contains("Variant", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(names, name => name.Contains("Sku", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(typeof(ProductCategory).GetProperties(), property => property.Name is "ParentId" or "Children");
    }

    [Fact]
    public void ImageRebuildAcquiresMultiProductLocksInDeterministicOrder()
    {
        var source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(),
            "src",
            "ToyStore.Infrastructure",
            "Persistence",
            "ApplicationDbContext.cs"));
        var distinct = source.IndexOf(".Distinct()", StringComparison.Ordinal);
        var order = source.IndexOf(".Order()", distinct, StringComparison.Ordinal);
        var materialize = source.IndexOf(".ToArray()", order, StringComparison.Ordinal);

        Assert.True(distinct >= 0 && order > distinct && materialize > order);
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "ToyStore.sln")))
            {
                return directory.FullName;
            }
        }

        throw new DirectoryNotFoundException("Could not locate ToyStore.sln.");
    }

    private static IEnumerable<Type> FlattenGenericArguments(Type type) =>
        type.IsGenericType
            ? type.GetGenericArguments().SelectMany(FlattenGenericArguments).Append(type)
            : [type];
}
