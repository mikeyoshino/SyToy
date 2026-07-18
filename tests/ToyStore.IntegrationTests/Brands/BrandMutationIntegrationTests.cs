using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.ArchiveBrand;
using ToyStore.Application.Brands.CreateBrand;
using ToyStore.Application.Brands.UpdateBrand;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Brands;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class BrandMutationIntegrationTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task ConcurrentNormalizedCreateYieldsOneSuccessOneTypedDuplicateAndNoMediaLeak()
    {
        await using var fixture = await CreateFixtureAsync();
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = 0;

        async Task<Result<BrandMutationResult>> CreateAsync(string displayName, string englishName)
        {
            if (Interlocked.Increment(ref entered) == 2)
            {
                ready.TrySetResult();
            }

            await ready.Task.WaitAsync(TestContext.Current.CancellationToken);
            return await fixture.CreateAsync(new CreateBrandCommand(
                displayName,
                englishName,
                Upload()));
        }

        var results = await Task.WhenAll(
            CreateAsync("  แบรนด์พร้อมกัน  ", "Concurrent First"),
            CreateAsync("แบรนด์พร้อมกัน", "Concurrent Second"));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            BrandErrors.DuplicateDisplayName,
            Assert.Single(results, result => result.IsFailure).Error);
        Assert.Equal(1, fixture.Storage.CommitCount);
        Assert.Equal(1, fixture.Storage.DiscardCount);
        Assert.Single(fixture.Storage.CommittedKeys);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(1, await dbContext.Brands.CountAsync(
            brand => brand.DisplayName == "แบรนด์พร้อมกัน",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentReplacementUpdatesYieldOneSuccessOneStaleAndDeleteOnlyOldMedia()
    {
        await using var fixture = await CreateFixtureAsync();
        var brand = await fixture.SeedBrandAsync("race-old");
        var commands = new[]
        {
            new UpdateBrandCommand(brand.Id, brand.Version, "ชื่อหนึ่ง", "First Update", Upload()),
            new UpdateBrandCommand(brand.Id, brand.Version, "ชื่อสอง", "Second Update", Upload()),
        };

        var results = await Task.WhenAll(commands.Select(fixture.UpdateAsync));

        Assert.Single(results, result => result.IsSuccess);
        Assert.Equal(
            BrandErrors.StaleVersion,
            Assert.Single(results, result => result.IsFailure).Error);
        Assert.Equal(1, fixture.Storage.CommitCount);
        Assert.Equal(1, fixture.Storage.DiscardCount);
        Assert.Equal(["race-old"], fixture.Storage.DeletedKeys);
        var winningKey = Assert.Single(fixture.Storage.CommittedKeys);
        Assert.NotEqual("race-old", winningKey);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await dbContext.Brands.AsNoTracking().SingleAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, reloaded.Version);
        Assert.Equal(winningKey, reloaded.Image!.StorageKey);
    }

    [Fact]
    public async Task UpdateArchiveRaceHasOneWinnerAndNeverDeletesReferencedCurrentMedia()
    {
        await using var fixture = await CreateFixtureAsync();
        var brand = await fixture.SeedBrandAsync("update-archive-old");

        var updateTask = fixture.UpdateAsync(new UpdateBrandCommand(
            brand.Id,
            brand.Version,
            "ชื่อที่แก้",
            "Updated Brand",
            Upload()));
        var archiveTask = fixture.ArchiveAsync(new ArchiveBrandCommand(
            brand.Id,
            brand.Version));
        var results = await Task.WhenAll(updateTask, archiveTask);

        Assert.Single(results, result => result.IsSuccess);
        var loser = Assert.Single(results, result => result.IsFailure);
        Assert.Contains(loser.Error, new[] { BrandErrors.StaleVersion, BrandErrors.Archived });
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await dbContext.Brands.AsNoTracking().SingleAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(2, reloaded.Version);
        Assert.Contains(reloaded.Image!.StorageKey, fixture.Storage.CommittedKeys);
        Assert.DoesNotContain(reloaded.Image.StorageKey, fixture.Storage.DeletedKeys);
    }

    [Fact]
    public async Task ArchiveWithoutProductReferencesPreservesImage()
    {
        await using var fixture = await CreateFixtureAsync();
        var brand = await fixture.SeedBrandAsync("unreferenced-old");

        var result = await fixture.ArchiveAsync(
            new ArchiveBrandCommand(brand.Id, brand.Version));

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Storage.DeletedKeys);
        Assert.Contains("unreferenced-old", fixture.Storage.CommittedKeys);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var verificationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verificationContext.Brands.AsNoTracking().SingleAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(CatalogReferenceStatus.Archived, reloaded.Status);
        Assert.Equal("unreferenced-old", reloaded.Image!.StorageKey);
        Assert.False(await verificationContext.Products.AnyAsync(
            product => product.BrandId == brand.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ArchiveWithManyProductReferencesPreservesImageAndEveryForeignKey()
    {
        await using var fixture = await CreateFixtureAsync();
        var brand = await fixture.SeedBrandAsync("referenced-old");
        var productIds = Enumerable.Range(1, 3)
            .Select(_ => Guid.NewGuid())
            .ToArray();
        await using (var seedScope = fixture.Factory.Services.CreateAsyncScope())
        {
            var seedContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            foreach (var (productId, index) in productIds.Select((id, index) => (id, index)))
            {
                seedContext.Products.Add(Product.CreateInStock(
                    productId,
                    $"สินค้าที่อ้างอิง {index}",
                    $"Referenced Product {index}",
                    "รายละเอียด",
                    $"referenced-product-{index}",
                    CatalogSeedIds.ArtToyCategory,
                    brand.Id,
                    CatalogSeedIds.UnknownUniverse,
                    InStockOffer.Create(Money.Create(100 + index)),
                    UtcNow,
                    "test"));
            }

            await seedContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var result = await fixture.ArchiveAsync(
            new ArchiveBrandCommand(brand.Id, brand.Version));

        Assert.True(result.IsSuccess);
        Assert.Empty(fixture.Storage.DeletedKeys);
        Assert.Contains("referenced-old", fixture.Storage.CommittedKeys);
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        var verificationContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var reloaded = await verificationContext.Brands.AsNoTracking().SingleAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(CatalogReferenceStatus.Archived, reloaded.Status);
        Assert.Equal("referenced-old", reloaded.Image!.StorageKey);
        var persistedProductIds = await verificationContext.Products
            .Where(product => product.BrandId == brand.Id)
            .Select(product => product.Id)
            .Order()
            .ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(productIds.Order(), persistedProductIds);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff, 1, 2, 3]), "image/jpeg");

    private async Task<Fixture> CreateFixtureAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        var storage = new ConcurrentStorage();
        var scope = factory.Services.CreateScope();
        var sessionFactory = scope.ServiceProvider.GetRequiredService<IBrandMutationSessionFactory>();
        var coordinator = new MediaMutationCoordinator(
            storage,
            scope.ServiceProvider.GetRequiredService<IMediaReferenceVerifier>(),
            scope.ServiceProvider.GetRequiredService<IMediaCleanupRegistry>(),
            NullLogger<MediaMutationCoordinator>.Instance);
        var resolver = new CatalogCommitOutcomeResolver(
            NullLogger<CatalogCommitOutcomeResolver>.Instance);
        var timeProvider = new FixedTimeProvider();
        return new Fixture(
            factory,
            scope,
            storage,
            new CreateBrandHandler(sessionFactory, coordinator, timeProvider),
            new UpdateBrandHandler(sessionFactory, coordinator, resolver, timeProvider),
            new ArchiveBrandHandler(sessionFactory, resolver, timeProvider));
    }

    private sealed class Fixture(
        ToyStoreWebApplicationFactory factory,
        IServiceScope scope,
        ConcurrentStorage storage,
        CreateBrandHandler createHandler,
        UpdateBrandHandler updateHandler,
        ArchiveBrandHandler archiveHandler) : IAsyncDisposable
    {
        public ToyStoreWebApplicationFactory Factory { get; } = factory;

        public ConcurrentStorage Storage { get; } = storage;

        public Task<Result<BrandMutationResult>> CreateAsync(CreateBrandCommand command) =>
            AuthorizeAsync(command, createHandler.Handle);

        public Task<Result<BrandMutationResult>> UpdateAsync(UpdateBrandCommand command) =>
            AuthorizeAsync(command, updateHandler.Handle);

        public Task<Result<BrandMutationResult>> ArchiveAsync(ArchiveBrandCommand command) =>
            AuthorizeAsync(command, archiveHandler.Handle);

        public async Task<Brand> SeedBrandAsync(string key)
        {
            Storage.AddCommitted(key);
            await using var seedScope = Factory.Services.CreateAsyncScope();
            var dbContext = seedScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var brand = Brand.CreateWithImage(
                Guid.NewGuid(),
                $"แบรนด์ {key}",
                $"Brand {key}",
                CatalogSlugGenerator.GenerateBase($"Brand {key}"),
                CatalogMediaReference.Create(key, $"/media/{key}.webp", $"รูป {key}"),
                UtcNow,
                "test");
            dbContext.Brands.Add(brand);
            await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
            return brand;
        }

        public async ValueTask DisposeAsync()
        {
            scope.Dispose();
            await Factory.DisposeAsync();
        }

        private static async Task<Result<BrandMutationResult>> AuthorizeAsync<TCommand>(
            TCommand command,
            Func<TCommand, CancellationToken, Task<Result<BrandMutationResult>>> handle)
            where TCommand : AuthorizedBrandMutationRequest<Result<BrandMutationResult>>
        {
            var authorization = new AuthorizationBehavior<TCommand, Result<BrandMutationResult>>(
                new StubAuthorization());
            return await authorization.Handle(
                command,
                cancellationToken => handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class ConcurrentStorage : IFileStorage
    {
        private readonly ConcurrentDictionary<string, byte> committed = new();
        private readonly ConcurrentDictionary<string, byte> staged = new();
        private readonly ConcurrentQueue<string> deleted = new();
        private int sequence;
        private int commitCount;
        private int discardCount;

        public int CommitCount => commitCount;

        public int DiscardCount => discardCount;

        public string[] CommittedKeys => committed.Keys.Order().ToArray();

        public string[] DeletedKeys => deleted.ToArray();

        public void AddCommitted(string key) => committed[key] = 0;

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            var key = $"brands/{Interlocked.Increment(ref sequence)}.jpg";
            staged[key] = 0;
            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(
                    $"batch-{key}",
                    [new StagedMedia($"batch-{key}", key, $"/media/{key}", "image/jpeg", 6)])));
        }

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
        {
            foreach (var media in batch.Media)
            {
                Assert.True(staged.TryRemove(media.StorageKey, out _));
                committed[media.StorageKey] = 0;
            }

            Interlocked.Increment(ref commitCount);
            return Task.CompletedTask;
        }

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken)
        {
            var key = batchToken["batch-".Length..];
            staged.TryRemove(key, out _);
            Interlocked.Increment(ref discardCount);
            return Task.CompletedTask;
        }

        public Task DeleteCommittedAsync(
            IReadOnlyCollection<string> storageKeys,
            CancellationToken cancellationToken)
        {
            foreach (var key in storageKeys)
            {
                committed.TryRemove(key, out _);
                deleted.Enqueue(key);
            }

            return Task.CompletedTask;
        }

        public Task<StoredMediaRead?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken) => Task.FromResult<StoredMediaRead?>(null);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow.AddMinutes(5);
    }

    private sealed class StubAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, "admin-1"));
    }
}
