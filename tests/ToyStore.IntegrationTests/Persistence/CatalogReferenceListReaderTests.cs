using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToyStore.Application.Brands;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogReferenceListReaderTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task EqualVersionWithConflictingEvidenceIsNotConfirmedAbsent()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var brandId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        Brand persisted;
        await using (var seedScope = factory.Services.CreateAsyncScope())
        {
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            persisted = Brand.Create(
                brandId,
                "แบรนด์จริง",
                "Persisted Brand",
                CatalogSlug.Create("persisted-brand"),
                now,
                "test");
            dbContext.Brands.Add(persisted);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var conflicting = Brand.Create(
            brandId,
            "แบรนด์ที่ขัดแย้ง",
            "Conflicting Brand",
            CatalogSlug.Create("conflicting-brand"),
            now,
            "test");
        await using var scope = factory.Services.CreateAsyncScope();
        var mutationFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();

        var verification = await mutationFactory.VerifyCommitAsync(
            BrandMutationEvidence.Capture(conflicting),
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitVerification.Inconsistent, verification.Outcome);
        Assert.False(verification.HasAuthoritativeState);
    }

    [Fact]
    public async Task ReadersUseIndependentNoTrackingContextsAndProjectCountsWithClampedPages()
    {
        await using var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var brandId = Guid.Parse("73000000-0000-0000-0000-000000000001");
        await SeedReferencesAsync(factory, brandId);

        var contextFactory = new RecordingContextFactory(postgreSql.ConnectionString);
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = postgreSql.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(contextFactory);
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var scopedContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var scopedBrand = await scopedContext.Brands.SingleAsync(
            brand => brand.Id == brandId,
            TestContext.Current.CancellationToken);
        scopedBrand.UpdateDetails(
            "ชื่อที่ยังไม่บันทึก",
            "Unsaved Scoped Name",
            CatalogSlug.Create("unsaved-scoped-name"),
            scopedBrand.UpdatedAtUtc.AddMinutes(1),
            "test-scoped");

        var brandReader = scope.ServiceProvider.GetRequiredService<IBrandListReader>();
        var universeReader = scope.ServiceProvider.GetRequiredService<IUniverseListReader>();
        var mutationFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var mutation = await mutationFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        var mutationReady = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var mutationTask = mutation.ExecuteOnceAsync<string>(async cancellationToken =>
        {
            var mutationBrand = await mutation.FindAsync(brandId, cancellationToken);
            Assert.NotNull(mutationBrand);
            mutationBrand.UpdateDetails(
                "ชื่อที่ยังไม่ commit",
                "Uncommitted Mutation Name",
                CatalogSlug.Create("uncommitted-mutation-name"),
                mutationBrand.UpdatedAtUtc.AddMinutes(1),
                "test-mutation");
            mutationReady.TrySetResult();
            await releaseMutation.Task.WaitAsync(cancellationToken);
            return Result<string>.Failure(new Error(
                "Test.Rollback",
                "ย้อนกลับข้อมูลทดสอบ",
                ErrorType.Conflict));
        }, TestContext.Current.CancellationToken);
        await mutationReady.Task.WaitAsync(TestContext.Current.CancellationToken);
        var contextsBeforeReads = contextFactory.CreatedContexts.ToHashSet();

        var brandRequest = new BrandListReadRequest(
            NormalizedSearch: null,
            CatalogReferenceStatus.Active,
            PageNumber: 99,
            PageSize: 20);
        var universeRequest = new UniverseListReadRequest(
            NormalizedSearch: null,
            CatalogReferenceStatus.Active,
            PageNumber: 1,
            PageSize: 20);

        var firstBrandRead = brandReader.ReadAsync(
            brandRequest,
            TestContext.Current.CancellationToken);
        var secondBrandRead = brandReader.ReadAsync(
            brandRequest,
            TestContext.Current.CancellationToken);
        var universeRead = universeReader.ReadAsync(
            universeRequest,
            TestContext.Current.CancellationToken);
        try
        {
            await Task.WhenAll(firstBrandRead, secondBrandRead, universeRead);
        }
        finally
        {
            releaseMutation.TrySetResult();
        }

        var firstBrandPage = await firstBrandRead;
        var secondBrandPage = await secondBrandRead;
        var universePage = await universeRead;
        var mutationExecution = await mutationTask;
        var listContexts = contextFactory.CreatedContexts
            .Where(context => !contextsBeforeReads.Contains(context))
            .ToArray();

        Assert.Equal(1, firstBrandPage.EffectivePageNumber);
        Assert.Equal(1, firstBrandPage.TotalCount);
        var brand = Assert.Single(firstBrandPage.Items);
        Assert.Equal("บันได", brand.DisplayName);
        Assert.Equal("Bandai", brand.EnglishName);
        Assert.Equal("bandai", brand.Slug);
        Assert.Equal("/media/brands/bandai.webp", brand.ImagePublicRelativeUrl);
        Assert.True(brand.CanBeUsedByPublishedProduct);
        Assert.Equal(2, brand.ProductReferenceCount);
        Assert.Equal(1, brand.Version);
        Assert.Equal(brand, Assert.Single(secondBrandPage.Items));

        var marvel = Assert.Single(
            universePage.Items,
            item => item.Id == CatalogSeedIds.MarvelUniverse);
        Assert.Equal(2, marvel.ProductReferenceCount);
        Assert.Equal(1, marvel.CharacterReferenceCount);
        Assert.False(marvel.CanBeUsedByPublishedProduct);
        Assert.Equal("ชื่อที่ยังไม่บันทึก", scopedBrand.DisplayName);
        Assert.Equal(EntityState.Modified, scopedContext.Entry(scopedBrand).State);
        Assert.Equal(
            "Test.Rollback",
            mutationExecution.Result.Error.Code);
        Assert.Equal(3, listContexts.Length);
        Assert.Equal(3, listContexts.Distinct().Count());
        Assert.Equal(
            contextFactory.CreatedContexts.Length,
            contextFactory.CreatedContexts.Distinct().Count());
        Assert.All(listContexts, context =>
        {
            Assert.Throws<ObjectDisposedException>(() => context.ChangeTracker.HasChanges());
        });
    }

    private static async Task SeedReferencesAsync(
        ToyStoreWebApplicationFactory factory,
        Guid brandId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var createdAtUtc = new DateTimeOffset(2026, 7, 17, 4, 0, 0, TimeSpan.Zero);
        var brand = Brand.CreateWithImage(
            brandId,
            "บันได",
            "Bandai",
            CatalogSlug.Create("bandai"),
            CatalogMediaReference.Create(
                "brands/bandai.webp",
                "/media/brands/bandai.webp",
                "โลโก้แบรนด์ บันได"),
            createdAtUtc,
            "test-seed");
        dbContext.Brands.Add(brand);
        dbContext.Characters.Add(Character.Create(
            Guid.Parse("74000000-0000-0000-0000-000000000001"),
            CatalogSeedIds.MarvelUniverse,
            "Spider-Man"));
        dbContext.Products.AddRange(
            CreateProduct(
                Guid.Parse("75000000-0000-0000-0000-000000000001"),
                "กันดั้มหนึ่ง",
                "Gundam One",
                "gundam-one",
                brandId,
                createdAtUtc),
            CreateProduct(
                Guid.Parse("75000000-0000-0000-0000-000000000002"),
                "กันดั้มสอง",
                "Gundam Two",
                "gundam-two",
                brandId,
                createdAtUtc.AddMinutes(1)));
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private static Product CreateProduct(
        Guid id,
        string displayName,
        string englishName,
        string slug,
        Guid brandId,
        DateTimeOffset createdAtUtc) =>
        Product.CreateInStock(
            id,
            displayName,
            englishName,
            "รายละเอียดสินค้า",
            slug,
            CatalogSeedIds.GundamCategory,
            brandId,
            CatalogSeedIds.MarvelUniverse,
            InStockOffer.Create(Money.Create(1000m)),
            createdAtUtc,
            "test-seed");

    private sealed class RecordingContextFactory(string connectionString)
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly object gate = new();
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .Options;
        private readonly List<ApplicationDbContext> contexts = [];

        public ApplicationDbContext[] CreatedContexts
        {
            get
            {
                lock (gate)
                {
                    return contexts.ToArray();
                }
            }
        }

        public ApplicationDbContext CreateDbContext()
        {
            var context = new ApplicationDbContext(options);
            lock (gate)
            {
                contexts.Add(context);
            }

            return context;
        }
    }
}
