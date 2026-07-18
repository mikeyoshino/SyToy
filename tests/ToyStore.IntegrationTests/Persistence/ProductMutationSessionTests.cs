using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class ProductMutationSessionTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task CreatePersistsFullProductAndZeroInventoryAtomicallyOnce()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "create");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var productId = Guid.NewGuid();
        var callbackCount = 0;

        var execution = await session.ExecuteOnceAsync(async cancellationToken =>
        {
            callbackCount++;
            await session.AcquireNamespaceLockAsync(cancellationToken);
            Assert.Null(await session.LockProductAsync(productId, cancellationToken));
            var readiness = await session.LockReferencesAsync(
                CatalogSeedIds.GundamCategory,
                references.BrandId,
                references.UniverseId,
                [references.CharacterId],
                cancellationToken);
            Assert.True(readiness.CategoryIsAllowedSeed);
            Assert.True(readiness.BrandIsReady);
            Assert.True(readiness.UniverseIsReady);
            Assert.True(readiness.CharacterIdsAreDistinct);
            Assert.Equal(references.CharacterId, Assert.Single(readiness.ExistingCharacterIds));
            Assert.False(await session.DisplayNameExistsAsync("PRODUCT CREATE", null, cancellationToken));
            Assert.False(await session.EnglishNameExistsAsync("PRODUCT CREATE", null, cancellationToken));
            var slug = await session.AllocateSlugAsync("Product Create", null, cancellationToken);
            var product = CreateProduct(productId, references, slug.Value, "create");
            var inventory = InventoryItem.Create(
                Guid.NewGuid(), product.Id, Guid.NewGuid(), 0,
                "สินค้าเริ่มต้น", "product-create", UtcNow, "admin");
            var evidence = ProductMutationEvidence.Capture(product, inventory);
            session.Add(product, inventory);
            return Result<ProductMutationEvidence>.Success(evidence);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, callbackCount);
        Assert.Equal(CatalogCommitOutcome.Committed, execution.CommitOutcome);
        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteOnceAsync(
            _ => Task.FromResult(Result<bool>.Success(true)),
            TestContext.Current.CancellationToken));
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var persisted = await db.Products.AsNoTracking()
            .Include(product => product.Images)
            .Include(product => product.Characters)
            .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);
        Assert.Equal(1, persisted.Version);
        Assert.Equal(ProductStatus.Draft, persisted.Status);
        Assert.Single(persisted.Images);
        Assert.Single(persisted.Characters);
        var inventoryItem = await db.InventoryItems.AsNoTracking().SingleAsync(
            item => item.ProductId == productId,
            TestContext.Current.CancellationToken);
        Assert.Equal(0, inventoryItem.OnHandQuantity);
        Assert.Equal(StockMovementType.InitialStock, await db.StockMovements
            .Where(movement => movement.ProductId == productId)
            .Select(movement => movement.Type)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TypedFailureRollsBackProductInventoryAndMovement()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "rollback");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken);
        var productId = Guid.NewGuid();

        var execution = await session.ExecuteOnceAsync(async cancellationToken =>
        {
            await session.AcquireNamespaceLockAsync(cancellationToken);
            _ = await session.LockProductAsync(productId, cancellationToken);
            _ = await session.LockReferencesAsync(
                CatalogSeedIds.GundamCategory,
                references.BrandId,
                references.UniverseId,
                [references.CharacterId],
                cancellationToken);
            var product = CreateProduct(productId, references, "rollback-product", "rollback");
            var inventory = InventoryItem.Create(
                Guid.NewGuid(), product.Id, Guid.NewGuid(), 2,
                "สินค้าเริ่มต้น", "rollback", UtcNow, "admin");
            session.Add(product, inventory);
            return Result<Guid>.Failure(new Error("Product.TestRollback", "rollback", ErrorType.Conflict));
        }, TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.DefinitelyRolledBack, execution.CommitOutcome);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Products.AnyAsync(
            product => product.Id == productId,
            TestContext.Current.CancellationToken));
        Assert.False(await db.InventoryItems.AnyAsync(
            item => item.ProductId == productId,
            TestContext.Current.CancellationToken));
        Assert.False(await db.StockMovements.AnyAsync(
            movement => movement.ProductId == productId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LockLoadsFullAggregateAndVersionRejectsStaleEfSave()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedProductAsync(factory, "version");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();
        await using (var session = await sessionFactory.OpenAsync(TestContext.Current.CancellationToken))
        {
            var execution = await session.ExecuteOnceAsync(async cancellationToken =>
            {
                await session.AcquireNamespaceLockAsync(cancellationToken);
                var product = Assert.IsType<Product>(
                    await session.LockProductAsync(seeded.ProductId, cancellationToken));
                Assert.Single(product.Images);
                Assert.Single(product.Characters);
                return Result<long>.Success(product.Version);
            }, TestContext.Current.CancellationToken);
            Assert.Equal(1, execution.Result.Value);
        }

        await using var firstScope = factory.Services.CreateAsyncScope();
        await using var secondScope = factory.Services.CreateAsyncScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var first = await LoadProductAsync(firstDb, seeded.ProductId);
        var second = await LoadProductAsync(secondDb, seeded.ProductId);
        UpdatePrice(first, 150, "first");
        UpdatePrice(second, 175, "second");

        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            secondDb.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task VerificationClassifiesCommittedMissingAndCorruptedCreationEvidence()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedProductAsync(factory, "verify");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();

        Assert.Equal(
            CatalogCommitVerification.Committed,
            (await sessionFactory.VerifyCommitAsync(
                seeded.Evidence,
                TestContext.Current.CancellationToken)).Outcome);

        await using (var updateScope = factory.Services.CreateAsyncScope())
        {
            var updateDb = updateScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var current = await LoadProductAsync(updateDb, seeded.ProductId);
            UpdatePrice(current, 125, "superseding-update");
            await updateDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var superseded = await sessionFactory.VerifyCommitAsync(
            seeded.Evidence,
            TestContext.Current.CancellationToken);
        Assert.Equal(CatalogCommitVerification.Superseded, superseded.Outcome);
        Assert.Equal(2, superseded.AuthoritativeState.IntendedVersion);
        var missingProduct = CreateProduct(
            Guid.NewGuid(), seeded.References, "missing-product", "missing");
        Assert.Equal(
            CatalogCommitVerification.NotCommitted,
            (await sessionFactory.VerifyCommitAsync(
                ProductMutationEvidence.Capture(missingProduct),
                TestContext.Current.CancellationToken)).Outcome);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.StockMovements
                .Where(movement => movement.Id == seeded.Evidence.InventoryEvidence.OperationId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        movement => movement.Reason,
                        "corrupted-initial-reason"),
                    TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            CatalogCommitVerification.Inconsistent,
            (await sessionFactory.VerifyCommitAsync(
                seeded.Evidence,
                TestContext.Current.CancellationToken)).Outcome);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.StockMovements
                .Where(movement => movement.ProductId == seeded.ProductId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            CatalogCommitVerification.Inconsistent,
            (await sessionFactory.VerifyCommitAsync(
                seeded.Evidence,
                TestContext.Current.CancellationToken)).Outcome);

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] =
                    "Host=127.0.0.1;Port=1;Database=unavailable_test;Username=test;Password=test;Timeout=1;Command Timeout=1;Pooling=false",
            })
            .Build());
        await using var unavailableProvider = services.BuildServiceProvider();
        Assert.Equal(
            CatalogCommitVerification.Unavailable,
            (await unavailableProvider
                .GetRequiredService<IProductMutationSessionFactory>()
                .VerifyCommitAsync(
                seeded.Evidence,
                CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task VerificationRejectsReplacedCreationEvidenceAfterProductWasSuperseded()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedProductAsync(factory, "replaced-evidence");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var product = await LoadProductAsync(db, seeded.ProductId);
            UpdatePrice(product, 150, "product-update");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            await db.StockMovements
                .Where(movement => movement.ProductId == seeded.ProductId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            await db.InventoryItems
                .Where(item => item.ProductId == seeded.ProductId)
                .ExecuteDeleteAsync(TestContext.Current.CancellationToken);
            var replacement = InventoryItem.Create(
                Guid.NewGuid(), seeded.ProductId, Guid.NewGuid(), 0,
                "สินค้าเริ่มต้น", "replacement-evidence", UtcNow, "replacement");
            db.InventoryItems.Add(replacement.Item);
            db.StockMovements.Add(replacement.InitialMovement);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            CatalogCommitVerification.Inconsistent,
            (await sessionFactory.VerifyCommitAsync(
                seeded.Evidence,
                TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task VerificationTreatsLegitimateLaterInventoryReceiveAndAdjustAsSuperseded()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedProductAsync(factory, "inventory-advanced");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var item = await db.InventoryItems.SingleAsync(
                current => current.ProductId == seeded.ProductId,
                TestContext.Current.CancellationToken);
            var received = item.ReceiveStock(
                Guid.NewGuid(), 3, "รับสินค้า", "after-create", 1,
                UtcNow.AddMinutes(1), "inventory-admin");
            db.StockMovements.Add(received);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
            var adjusted = item.AdjustStock(
                Guid.NewGuid(), -1, "ปรับสินค้า", "after-create-adjust", 2,
                UtcNow.AddMinutes(2), "inventory-admin");
            db.StockMovements.Add(adjusted);
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var verification = await sessionFactory.VerifyCommitAsync(
            seeded.Evidence,
            TestContext.Current.CancellationToken);
        Assert.Equal(CatalogCommitVerification.Superseded, verification.Outcome);
        Assert.Equal(1, verification.AuthoritativeState.IntendedVersion);
        Assert.Equal(3, verification.AuthoritativeState.InventoryEvidence.IntendedVersion);
        Assert.Equal(2, verification.AuthoritativeState.InventoryEvidence.IntendedOnHandQuantity);
    }

    [Fact]
    public async Task VerificationRejectsCorruptedInventoryCreationAudit()
    {
        await using var factory = await StartAndResetAsync();
        var seeded = await SeedProductAsync(factory, "corrupt-audit");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.InventoryItems
                .Where(item => item.ProductId == seeded.ProductId)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(item => item.CreatedBy, "corrupted-actor"),
                    TestContext.Current.CancellationToken);
        }

        Assert.Equal(
            CatalogCommitVerification.Inconsistent,
            (await sessionFactory.VerifyCommitAsync(
                seeded.Evidence,
                TestContext.Current.CancellationToken)).Outcome);
    }

    [Fact]
    public async Task CommitAcknowledgementFailureIsIndeterminateAndCallbackIsNeverReplayed()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "commit-unknown");
        var interceptor = new CommitAcknowledgementFailureInterceptor();
        await using var provider = CreateProvider(interceptor);
        var sessionFactory = provider.GetRequiredService<IProductMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        var productId = Guid.NewGuid();
        var callbacks = 0;

        var execution = await session.ExecuteOnceAsync(async cancellationToken =>
        {
            callbacks++;
            await session.AcquireNamespaceLockAsync(cancellationToken);
            Assert.Null(await session.LockProductAsync(productId, cancellationToken));
            _ = await session.LockReferencesAsync(
                CatalogSeedIds.GundamCategory,
                references.BrandId,
                references.UniverseId,
                [references.CharacterId],
                cancellationToken);
            var product = CreateProduct(
                productId, references, "product-commit-unknown", "commit-unknown");
            var inventory = InventoryItem.Create(
                Guid.NewGuid(), product.Id, Guid.NewGuid(), 0,
                "สินค้าเริ่มต้น", "commit-unknown", UtcNow, "admin");
            var evidence = ProductMutationEvidence.Capture(product, inventory);
            session.Add(product, inventory);
            return Result<ProductMutationEvidence>.Success(evidence);
        }, TestContext.Current.CancellationToken);

        Assert.Equal(1, callbacks);
        Assert.Equal(1, interceptor.InvocationCount);
        Assert.Equal(CatalogCommitOutcome.Indeterminate, execution.CommitOutcome);
        Assert.NotNull(execution.CommitFailure);
        Assert.Equal(
            CatalogCommitVerification.Committed,
            (await sessionFactory.VerifyCommitAsync(
                execution.Result.Value,
                CancellationToken.None)).Outcome);
    }

    [Fact]
    public async Task SaveFailureRollsBackProductInventoryAndInitialMovement()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "save-failure");
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var sessionFactory = provider.GetRequiredService<IProductMutationSessionFactory>();
        await using var session = await sessionFactory.OpenAsync(
            TestContext.Current.CancellationToken);
        var productId = Guid.NewGuid();

        await Assert.ThrowsAsync<InjectedSaveException>(() => session.ExecuteOnceAsync(
            async cancellationToken =>
            {
                await session.AcquireNamespaceLockAsync(cancellationToken);
                _ = await session.LockProductAsync(productId, cancellationToken);
                _ = await session.LockReferencesAsync(
                    CatalogSeedIds.GundamCategory,
                    references.BrandId,
                    references.UniverseId,
                    [references.CharacterId],
                    cancellationToken);
                var product = CreateProduct(
                    productId, references, "product-save-failure", "save-failure");
                var inventory = InventoryItem.Create(
                    Guid.NewGuid(), product.Id, Guid.NewGuid(), 3,
                    "สินค้าเริ่มต้น", "save-failure", UtcNow, "admin");
                session.Add(product, inventory);
                return Result<bool>.Success(true);
            },
            TestContext.Current.CancellationToken));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Products.AnyAsync(
            product => product.Id == productId,
            TestContext.Current.CancellationToken));
        Assert.False(await db.InventoryItems.AnyAsync(
            item => item.ProductId == productId,
            TestContext.Current.CancellationToken));
        Assert.False(await db.StockMovements.AnyAsync(
            movement => movement.ProductId == productId,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task LockedProductReferencesSerializeAnOverlappingBrandArchive()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "archive-race");
        var sessionFactory = factory.Services.GetRequiredService<IProductMutationSessionFactory>();
        var referencesLocked = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseMutation = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var archiveSaving = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);

        async Task<CatalogMutationExecution<bool>> HoldReferencesAsync()
        {
            await using var session = await sessionFactory.OpenAsync(
                TestContext.Current.CancellationToken);
            return await session.ExecuteOnceAsync(async cancellationToken =>
            {
                await session.AcquireNamespaceLockAsync(cancellationToken);
                _ = await session.LockProductAsync(Guid.NewGuid(), cancellationToken);
                var readiness = await session.LockReferencesAsync(
                    CatalogSeedIds.GundamCategory,
                    references.BrandId,
                    references.UniverseId,
                    [references.CharacterId],
                    cancellationToken);
                Assert.True(readiness.BrandIsReady);
                referencesLocked.TrySetResult();
                await releaseMutation.Task.WaitAsync(cancellationToken);
                return Result<bool>.Success(true);
            }, TestContext.Current.CancellationToken);
        }

        async Task ArchiveBrandAsync()
        {
            await referencesLocked.Task.WaitAsync(TestContext.Current.CancellationToken);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var brand = await db.Brands.SingleAsync(
                current => current.Id == references.BrandId,
                TestContext.Current.CancellationToken);
            brand.Archive(UtcNow.AddMinutes(1), "archive-admin");
            archiveSaving.TrySetResult();
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var mutation = HoldReferencesAsync();
        var archive = ArchiveBrandAsync();
        await archiveSaving.Task.WaitAsync(TestContext.Current.CancellationToken);
        await Task.Delay(150, TestContext.Current.CancellationToken);
        Assert.False(archive.IsCompleted);
        releaseMutation.TrySetResult();

        Assert.Equal(CatalogCommitOutcome.Committed, (await mutation).CommitOutcome);
        await archive;
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private async Task<ToyStoreWebApplicationFactory> StartAndResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private ServiceProvider CreateProvider(params IInterceptor[] interceptors)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = postgreSql.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(
            new InterceptedContextFactory(postgreSql.ConnectionString, interceptors));
        return services.BuildServiceProvider();
    }

    private static async Task<ReferenceIds> SeedReferencesAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = Brand.CreateWithImage(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"), Media($"brand-{suffix}"), UtcNow, "test");
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(), $"จักรวาล {suffix}", $"Universe {suffix}",
            CatalogSlug.Create($"universe-{suffix}"), Media($"universe-{suffix}"), UtcNow, "test");
        var character = Character.Create(Guid.NewGuid(), universe.Id, $"Character {suffix}");
        db.AddRange(brand, universe, character);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new ReferenceIds(brand.Id, universe.Id, character.Id);
    }

    private static async Task<SeededProduct> SeedProductAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix)
    {
        var references = await SeedReferencesAsync(factory, suffix);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = CreateProduct(Guid.NewGuid(), references, $"product-{suffix}", suffix);
        var inventory = InventoryItem.Create(
            Guid.NewGuid(), product.Id, Guid.NewGuid(), 0,
            "สินค้าเริ่มต้น", $"initial-{suffix}", UtcNow, "test");
        db.Products.Add(product);
        db.InventoryItems.Add(inventory.Item);
        db.StockMovements.Add(inventory.InitialMovement);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededProduct(
            product.Id,
            references,
            ProductMutationEvidence.Capture(product, inventory));
    }

    private static Product CreateProduct(
        Guid id,
        ReferenceIds references,
        string slug,
        string suffix) => Product.CreateInStock(
            id,
            $"สินค้า {suffix}",
            $"Product {suffix}",
            "รายละเอียด",
            slug,
            CatalogSeedIds.GundamCategory,
            references.BrandId,
            references.UniverseId,
            InStockOffer.Create(Money.Create(100)),
            [new ProductImageDefinition(
                Guid.NewGuid(), $"products/{suffix}/main.webp",
                $"/media/products/{suffix}/main.webp", $"สินค้า {suffix}")],
            [references.CharacterId],
            UtcNow,
            "test");

    private static CatalogMediaReference Media(string key) =>
        CatalogMediaReference.Create($"catalog/{key}.webp", $"/media/catalog/{key}.webp", key);

    private static Task<Product> LoadProductAsync(ApplicationDbContext db, Guid productId) =>
        db.Products.Include(product => product.Images).Include(product => product.Characters)
            .SingleAsync(product => product.Id == productId, TestContext.Current.CancellationToken);

    private static void UpdatePrice(Product product, decimal price, string actor) =>
        product.UpdateDraftInStock(
            product.DisplayName,
            product.EnglishName,
            product.Description,
            product.Slug,
            product.ProductCategoryId,
            product.BrandId,
            product.UniverseId,
            InStockOffer.Create(Money.Create(price)),
            product.Images.OrderBy(image => image.SortOrder).Select(image =>
                new ProductImageDefinition(
                    image.Id, image.StorageKey, image.PublicRelativeUrl, image.AltText)).ToArray(),
            product.Characters.Select(link => link.CharacterId).ToArray(),
            product.Version,
            UtcNow.AddMinutes(1),
            actor);

    private sealed record ReferenceIds(Guid BrandId, Guid UniverseId, Guid CharacterId);

    private sealed record SeededProduct(
        Guid ProductId,
        ReferenceIds References,
        ProductMutationEvidence Evidence);

    private sealed class InterceptedContextFactory(
        string connectionString,
        params IInterceptor[] interceptors)
        : IDbContextFactory<ApplicationDbContext>
    {
        private readonly DbContextOptions<ApplicationDbContext> options =
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptors)
                .Options;

        public ApplicationDbContext CreateDbContext() => new(options);
    }

    private sealed class CommitAcknowledgementFailureInterceptor : DbTransactionInterceptor
    {
        public int InvocationCount { get; private set; }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromException(new InjectedCommitAcknowledgementException());
        }
    }

    private sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromException<InterceptionResult<int>>(new InjectedSaveException());
    }

    private sealed class InjectedCommitAcknowledgementException : Exception;

    private sealed class InjectedSaveException : Exception;
}
