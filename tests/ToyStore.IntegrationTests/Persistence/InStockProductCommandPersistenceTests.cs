using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Application.Products.ArchiveProduct;
using ToyStore.Application.Products.CreateInStockProduct;
using ToyStore.Application.Products.CreatePreOrderProduct;
using ToyStore.Application.Products.PublishProduct;
using ToyStore.Application.Products.UpdateDraftInStockProduct;
using ToyStore.Application.Products.UpdateDraftPreOrderProduct;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class InStockProductCommandPersistenceTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task PreOrderCreateUpdateAndPublishCreatesCapacityAtomically()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "preorder-flow");
        var harness = CreateHarness(factory);
        var created = await harness.CreatePreOrderAsync(PreOrderCommand(
            "preorder-flow", references, [new UploadProductMediaSlot(Upload())]));
        Assert.True(created.IsSuccess);

        var updated = await harness.UpdatePreOrderAsync(new UpdateDraftPreOrderProductCommand(
            created.Value.Id, created.Value.Version, "พรีออเดอร์แก้ไข", "Preorder Updated",
            "รายละเอียดแก้ไข", CatalogSeedIds.GundamCategory, references.BrandId,
            references.UniverseId, [references.CharacterId], 2400, 600,
            new DateOnly(2026, 12, 15), 1, 2027, 12, 3, 7,
            created.Value.Images.Select(x => (ProductMediaPlanSlot)new RetainedProductMediaSlot(x.Id)).ToArray()));
        Assert.True(updated.IsSuccess);

        var published = await harness.PublishAsync(new PublishProductCommand(
            updated.Value.Id, updated.Value.Version));
        Assert.True(published.IsSuccess);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var capacity = await db.PreOrderCapacities.AsNoTracking().SingleAsync(
            x => x.ProductId == published.Value.Id, TestContext.Current.CancellationToken);
        var movement = await db.PreOrderCapacityMovements.AsNoTracking().SingleAsync(
            x => x.CapacityId == capacity.Id, TestContext.Current.CancellationToken);
        Assert.Equal(12, capacity.TotalCapacity);
        Assert.Equal(PreOrderCapacityMovementType.InitialCapacity, movement.Type);
        Assert.Equal(12, movement.Quantity);
    }

    [Fact]
    public async Task PreOrderPublishSaveFailureRollsBackProductAndCapacity()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "preorder-rollback");
        var created = await CreateHarness(factory).CreatePreOrderAsync(PreOrderCommand(
            "preorder-rollback", references, [new UploadProductMediaSlot(Upload())]));
        Assert.True(created.IsSuccess);
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var failingStorage = new FakeStorage();
        var failing = CreateHarness(provider, failingStorage);

        await Assert.ThrowsAsync<InjectedSaveException>(() => failing.PublishAsync(
            new PublishProductCommand(created.Value.Id, created.Value.Version)));

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.AsNoTracking().Include(x => x.Images).SingleAsync(
            x => x.Id == created.Value.Id, TestContext.Current.CancellationToken);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(created.Value.Images.Select(x => x.PublicRelativeUrl),
            product.Images.OrderBy(x => x.SortOrder).Select(x => x.PublicRelativeUrl));
        Assert.Empty(failingStorage.DeletedKeys);
        Assert.False(await db.PreOrderCapacities.AnyAsync(
            x => x.ProductId == created.Value.Id, TestContext.Current.CancellationToken));
        Assert.False(await db.PreOrderCapacityMovements.AnyAsync(
            x => x.ProductId == created.Value.Id, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreOrderCreateSaveFailureDeletesCommittedUploadAndLeavesNoPartialRows()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "preorder-create-rollback");
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var storage = new FakeStorage();
        var harness = CreateHarness(provider, storage);
        var command = PreOrderCommand(
            "preorder-create-rollback", references, [new UploadProductMediaSlot(Upload())]);

        await Assert.ThrowsAsync<InjectedSaveException>(() => harness.CreatePreOrderAsync(command));

        Assert.Equal(1, storage.CommitCount);
        Assert.Single(storage.DeletedKeys);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Products.AnyAsync(
            x => x.EnglishName == command.EnglishName, TestContext.Current.CancellationToken));
        Assert.False(await db.PreOrderCapacities.AnyAsync(
            x => x.CreatedBy == "admin-1", TestContext.Current.CancellationToken));
        Assert.False(await db.PreOrderCapacityMovements.AnyAsync(
            x => x.Actor == "admin-1", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreOrderHandlersDefensivelyReturnTypedThaiTemporalFailures()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "preorder-temporal-defense");
        var harness = CreateHarness(factory);
        var past = PreOrderCommand("preorder-past", references, []) with
        {
            CloseDate = new DateOnly(2026, 7, 16),
            EstimatedArrivalMonth = 7,
            EstimatedArrivalYear = 2026,
        };

        var createFailure = await harness.CreatePreOrderAsync(past);
        Assert.Equal(ProductErrors.InvalidInput, createFailure.Error);
        Assert.Contains(createFailure.ValidationFailures, x =>
            x.PropertyName == nameof(CreatePreOrderProductCommand.CloseDate));

        var created = await harness.CreatePreOrderAsync(PreOrderCommand(
            "preorder-update-defense", references, []));
        Assert.True(created.IsSuccess);
        var updateFailure = await harness.UpdatePreOrderAsync(new UpdateDraftPreOrderProductCommand(
            created.Value.Id, created.Value.Version, created.Value.DisplayName,
            created.Value.EnglishName, created.Value.Description, created.Value.ProductCategoryId,
            created.Value.BrandId, created.Value.UniverseId, created.Value.CharacterIds,
            2000, 500, new DateOnly(2026, 7, 16), 7, 2026, 10, 2, 7, []));
        Assert.Equal(ProductErrors.InvalidInput, updateFailure.Error);
        Assert.Contains(updateFailure.ValidationFailures, x =>
            x.PropertyName == nameof(UpdateDraftPreOrderProductCommand.CloseDate));
    }
    [Theory]
    [InlineData(0)]
    [InlineData(9)]
    public async Task CreateCommitsProductInventoryAndOneInitialMovementAtomically(int initialStock)
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "create");
        var harness = CreateHarness(factory);

        var result = await harness.CreateAsync(new CreateInStockProductCommand(
            $"สินค้า create {initialStock}",
            $"Product create {initialStock}",
            "รายละเอียด",
            CatalogSeedIds.GundamCategory,
            references.BrandId,
            references.UniverseId,
            [references.CharacterId],
            1290,
            initialStock,
            []));

        Assert.True(result.IsSuccess);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.AsNoTracking()
            .Include(current => current.Characters)
            .SingleAsync(current => current.Id == result.Value.Id, TestContext.Current.CancellationToken);
        var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(
            item => item.ProductId == product.Id,
            TestContext.Current.CancellationToken);
        var movement = await db.StockMovements.AsNoTracking().SingleAsync(
            item => item.ProductId == product.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(initialStock, inventory.OnHandQuantity);
        Assert.Equal(StockMovementType.InitialStock, movement.Type);
        Assert.Equal(initialStock, movement.QuantityDelta);
        Assert.Equal(1, product.Version);
        Assert.Equal(references.CharacterId, Assert.Single(product.Characters).CharacterId);
    }

    [Fact]
    public async Task UpdateReplacesDetailsMediaAndCharactersButLeavesStockAndMovementsUntouched()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedReferencesAsync(factory, "first");
        var second = await SeedReferencesAsync(factory, "second");
        var harness = CreateHarness(factory);
        var created = await harness.CreateAsync(new CreateInStockProductCommand(
            "สินค้าเดิม",
            "Original Product",
            "รายละเอียดเดิม",
            CatalogSeedIds.ArtToyCategory,
            first.BrandId,
            first.UniverseId,
            [first.CharacterId],
            100,
            6,
            [new UploadProductMediaSlot(Upload()), new UploadProductMediaSlot(Upload())]));
        Assert.True(created.IsSuccess);
        var retainedId = created.Value.Images[1].Id;

        var updated = await harness.UpdateAsync(new UpdateDraftInStockProductCommand(
            created.Value.Id,
            created.Value.Version,
            "สินค้าแก้ไข",
            "Updated Product",
            "รายละเอียดแก้ไข",
            CatalogSeedIds.GundamCategory,
            second.BrandId,
            second.UniverseId,
            [second.CharacterId],
            250,
            [new RetainedProductMediaSlot(retainedId), new UploadProductMediaSlot(Upload())]));

        Assert.True(updated.IsSuccess);
        Assert.Equal(2, updated.Value.Version);
        Assert.Equal(retainedId, updated.Value.Images[0].Id);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(
            item => item.ProductId == created.Value.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(6, inventory.OnHandQuantity);
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.ProductId == created.Value.Id,
            TestContext.Current.CancellationToken));
        Assert.Equal(second.CharacterId, await db.ProductCharacters
            .Where(link => link.ProductId == created.Value.Id)
            .Select(link => link.CharacterId)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Single(harness.Storage.DeletedKeys);
    }

    [Fact]
    public async Task LockedReferenceValidationRejectsArchivedAndWrongUniverseCharacters()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "invalid");
        var other = await SeedReferencesAsync(factory, "other");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var brand = await db.Brands.SingleAsync(
                current => current.Id == references.BrandId,
                TestContext.Current.CancellationToken);
            brand.Archive(UtcNow.AddMinutes(1), "admin");
            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        var harness = CreateHarness(factory);
        var archived = await harness.CreateAsync(Command(
            "archived",
            references.BrandId,
            references.UniverseId,
            references.CharacterId));
        var wrongUniverse = await harness.CreateAsync(Command(
            "wrong-character",
            other.BrandId,
            other.UniverseId,
            references.CharacterId));

        Assert.Equal(ProductErrors.BrandUnavailable, archived.Error);
        Assert.Equal(ProductErrors.CharactersUnavailable, wrongUniverse.Error);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await verifyDb.Products.AnyAsync(
            product => product.EnglishName == "Product archived"
                || product.EnglishName == "Product wrong-character",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DraftCreateAndUpdateAcceptActiveReferencesWithoutCatalogMedia()
    {
        await using var factory = await StartAndResetAsync();
        var first = await SeedReferencesAsync(factory, "draft-no-media-1", includeMedia: false);
        var second = await SeedReferencesAsync(factory, "draft-no-media-2", includeMedia: false);
        var harness = CreateHarness(factory);

        var created = await harness.CreateAsync(Command(
            "draft-no-media",
            first.BrandId,
            first.UniverseId,
            first.CharacterId));
        Assert.True(created.IsSuccess);

        var updated = await harness.UpdateAsync(new UpdateDraftInStockProductCommand(
            created.Value.Id,
            created.Value.Version,
            "สินค้าไม่มี catalog media",
            created.Value.EnglishName,
            "รายละเอียดแก้ไข",
            CatalogSeedIds.ArtToyCategory,
            second.BrandId,
            second.UniverseId,
            [second.CharacterId],
            175,
            []));

        Assert.True(updated.IsSuccess);
        Assert.Equal(second.BrandId, updated.Value.BrandId);
        Assert.Equal(second.UniverseId, updated.Value.UniverseId);
    }

    [Fact]
    public async Task DuplicateAndStaleUpdateReturnTypedFailuresWithoutChangingStock()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "typed");
        var harness = CreateHarness(factory);
        var first = await harness.CreateAsync(Command(
            "duplicate",
            references.BrandId,
            references.UniverseId,
            references.CharacterId));
        Assert.True(first.IsSuccess);

        var duplicate = await harness.CreateAsync(Command(
            "duplicate",
            references.BrandId,
            references.UniverseId,
            references.CharacterId));
        var stale = await harness.UpdateAsync(new UpdateDraftInStockProductCommand(
            first.Value.Id,
            first.Value.Version + 1,
            "เปลี่ยน",
            "Changed Product",
            "รายละเอียด",
            CatalogSeedIds.GundamCategory,
            references.BrandId,
            references.UniverseId,
            [references.CharacterId],
            200,
            []));

        Assert.Equal(ProductErrors.DuplicateDisplayName, duplicate.Error);
        Assert.Equal(ProductErrors.StaleVersion, stale.Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(0, await db.InventoryItems
            .Where(item => item.ProductId == first.Value.Id)
            .Select(item => item.OnHandQuantity)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.ProductId == first.Value.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ConcurrentDuplicateCreatesAndVersionedUpdatesSerializeToTypedOutcomes()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "race");
        var firstHarness = CreateHarness(factory);
        var secondHarness = CreateHarness(factory);
        var firstCommand = Command(
            "race",
            references.BrandId,
            references.UniverseId,
            references.CharacterId);
        var secondCommand = Command(
            "race",
            references.BrandId,
            references.UniverseId,
            references.CharacterId);

        var createResults = await Task.WhenAll(
            firstHarness.CreateAsync(firstCommand),
            secondHarness.CreateAsync(secondCommand));

        var created = Assert.Single(createResults, result => result.IsSuccess);
        var duplicate = Assert.Single(createResults, result => result.IsFailure);
        Assert.Equal(ProductErrors.DuplicateDisplayName, duplicate.Error);

        UpdateDraftInStockProductCommand Update(string displayName, decimal price) => new(
            created.Value.Id,
            created.Value.Version,
            displayName,
            "Product race",
            "รายละเอียด",
            CatalogSeedIds.GundamCategory,
            references.BrandId,
            references.UniverseId,
            [references.CharacterId],
            price,
            []);

        var updateResults = await Task.WhenAll(
            firstHarness.UpdateAsync(Update("สินค้าแก้ไขหนึ่ง", 200)),
            secondHarness.UpdateAsync(Update("สินค้าแก้ไขสอง", 300)));

        Assert.Single(updateResults, result => result.IsSuccess);
        Assert.Equal(
            ProductErrors.StaleVersion,
            Assert.Single(updateResults, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.Products
            .Where(product => product.Id == created.Value.Id)
            .Select(product => product.Version)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.ProductId == created.Value.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("create", "brand")]
    [InlineData("create", "universe")]
    [InlineData("update", "brand")]
    [InlineData("update", "universe")]
    public async Task ProductCommandAndReferenceArchiveRaceHasOnlySerializedBusinessOutcomes(
        string action,
        string referenceType)
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(
            factory,
            $"{action}-{referenceType}-archive-race");
        var harness = CreateHarness(factory);
        ProductMutationResult? existing = null;
        if (action == "update")
        {
            var seeded = await harness.CreateAsync(Command(
                $"seed-{referenceType}-race",
                references.BrandId,
                references.UniverseId,
                references.CharacterId));
            Assert.True(seeded.IsSuccess);
            existing = seeded.Value;
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ArchiveAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (referenceType == "brand")
            {
                var brand = await db.Brands.SingleAsync(
                    current => current.Id == references.BrandId,
                    TestContext.Current.CancellationToken);
                await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
                brand.Archive(UtcNow.AddMinutes(1), "archive-admin");
            }
            else
            {
                var universe = await db.Universes.SingleAsync(
                    current => current.Id == references.UniverseId,
                    TestContext.Current.CancellationToken);
                await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
                universe.Archive(UtcNow.AddMinutes(1), "archive-admin");
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        async Task<Result<ProductMutationResult>> MutateAsync()
        {
            await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
            if (action == "create")
            {
                return await harness.CreateAsync(Command(
                    $"create-{referenceType}-race",
                    references.BrandId,
                    references.UniverseId,
                    references.CharacterId));
            }

            return await harness.UpdateAsync(new UpdateDraftInStockProductCommand(
                existing!.Id,
                existing.Version,
                $"สินค้า update {referenceType} race",
                existing.EnglishName,
                existing.Description,
                existing.ProductCategoryId,
                references.BrandId,
                references.UniverseId,
                [references.CharacterId],
                existing.Price + 1,
                []));
        }

        var archive = ArchiveAsync();
        var mutation = MutateAsync();
        gate.TrySetResult();
        var result = await mutation;
        await archive;

        if (result.IsFailure)
        {
            Assert.Equal(
                referenceType == "brand"
                    ? ProductErrors.BrandUnavailable
                    : ProductErrors.UniverseUnavailable,
                result.Error);
        }
        else
        {
            Assert.True(result.IsSuccess);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var archived = referenceType == "brand"
            ? await verifyDb.Brands.Where(item => item.Id == references.BrandId)
                .Select(item => item.Status)
                .SingleAsync(TestContext.Current.CancellationToken)
            : await verifyDb.Universes.Where(item => item.Id == references.UniverseId)
                .Select(item => item.Status)
                .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(CatalogReferenceStatus.Archived, archived);
        if (action == "create")
        {
            Assert.Equal(
                result.IsSuccess,
                await verifyDb.Products.AnyAsync(
                    product => product.EnglishName == $"Product create-{referenceType}-race",
                    TestContext.Current.CancellationToken));
        }
        else
        {
            Assert.Equal(
                result.IsSuccess ? 2 : 1,
                await verifyDb.Products
                    .Where(product => product.Id == existing!.Id)
                    .Select(product => product.Version)
                    .SingleAsync(TestContext.Current.CancellationToken));
        }
    }

    [Fact]
    public async Task HandlerDeletesCommittedUploadWhenDatabaseSaveFailsAndRollsBackAllRows()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "handler-save-failure");
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var storage = new FakeStorage();
        var harness = CreateHarness(provider, storage);
        var command = Command(
            "handler-save-failure",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            Images = [new UploadProductMediaSlot(Upload())],
        };

        await Assert.ThrowsAsync<InjectedSaveException>(() => harness.CreateAsync(command));

        Assert.Equal(1, storage.CommitCount);
        Assert.Single(storage.DeletedKeys);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.False(await db.Products.AnyAsync(
            product => product.EnglishName == command.EnglishName,
            TestContext.Current.CancellationToken));
        Assert.False(await db.InventoryItems.AnyAsync(
            item => item.CreatedBy == "admin-1",
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HandlerVerifiesCommitAcknowledgementFailureWithoutDeletingReferencedUpload()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "handler-commit-unknown");
        var interceptor = new CommitAcknowledgementFailureInterceptor();
        await using var provider = CreateProvider(interceptor);
        var storage = new FakeStorage();
        var harness = CreateHarness(provider, storage);
        var command = Command(
            "handler-commit-unknown",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            Images = [new UploadProductMediaSlot(Upload())],
        };

        var result = await harness.CreateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, interceptor.InvocationCount);
        Assert.Equal(1, storage.CommitCount);
        Assert.Empty(storage.DeletedKeys);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.True(await db.Products.AnyAsync(
            product => product.Id == result.Value.Id,
            TestContext.Current.CancellationToken));
        Assert.True(await db.InventoryItems.AnyAsync(
            item => item.ProductId == result.Value.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishThenArchivePersistsLifecycleAuditAndLeavesInventoryAndMediaUntouched()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "lifecycle");
        var harness = CreateHarness(factory);
        var created = await harness.CreateAsync(Command(
            "lifecycle",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            InitialStock = 5,
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);
        var imageUrl = Assert.Single(created.Value.Images).PublicRelativeUrl;

        var published = await harness.PublishAsync(
            new PublishProductCommand(created.Value.Id, created.Value.Version));
        var archived = await harness.ArchiveAsync(
            new ArchiveProductCommand(published.Value.Id, published.Value.Version));

        Assert.True(published.IsSuccess);
        Assert.True(archived.IsSuccess);
        Assert.Equal(ProductStatus.Archived, archived.Value.Status);
        Assert.Equal(3, archived.Value.Version);
        Assert.Equal(imageUrl, Assert.Single(archived.Value.Images).PublicRelativeUrl);
        Assert.Empty(harness.Storage.DeletedKeys);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.AsNoTracking()
            .Include(item => item.Images)
            .SingleAsync(item => item.Id == created.Value.Id, TestContext.Current.CancellationToken);
        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Equal("admin-1", product.PublishedBy);
        Assert.Equal("admin-1", product.ArchivedBy);
        Assert.Single(product.Images);
        Assert.Equal(5, await db.InventoryItems
            .Where(item => item.ProductId == product.Id)
            .Select(item => item.OnHandQuantity)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.ProductId == product.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(false, false, "Product.PublishBrandUnavailable")]
    [InlineData(true, false, "Product.PublishUniverseUnavailable")]
    public async Task PublishRejectsActiveReferencesMissingRequiredCatalogMedia(
        bool brandHasMedia,
        bool universeHasMedia,
        string expectedCode)
    {
        await using var factory = await StartAndResetAsync();
        var suffix = $"publish-media-{brandHasMedia}-{universeHasMedia}"
            .ToLowerInvariant();
        var references = await SeedReferencesAsync(
            factory,
            suffix,
            includeMedia: false,
            brandMedia: brandHasMedia,
            universeMedia: universeHasMedia);
        var harness = CreateHarness(factory);
        var created = await harness.CreateAsync(Command(
            suffix,
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);

        var result = await harness.PublishAsync(
            new PublishProductCommand(created.Value.Id, created.Value.Version));

        Assert.Equal(expectedCode, result.Error.Code);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(ProductStatus.Draft, await db.Products
            .Where(product => product.Id == created.Value.Id)
            .Select(product => product.Status)
            .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishUsesExpectedVersionAndConcurrentLifecycleCommandsMutateOnce()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "lifecycle-race");
        var first = CreateHarness(factory);
        var second = CreateHarness(factory);
        var created = await first.CreateAsync(Command(
            "lifecycle-race",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);
        var staleVersion = created.Value.Version;
        var updated = await first.UpdateAsync(new UpdateDraftInStockProductCommand(
            created.Value.Id,
            created.Value.Version,
            created.Value.DisplayName,
            created.Value.EnglishName,
            "รายละเอียดที่แก้ไข",
            created.Value.ProductCategoryId,
            created.Value.BrandId,
            created.Value.UniverseId,
            created.Value.CharacterIds,
            created.Value.Price + 1,
            created.Value.Images.Select(image =>
                (ProductMediaPlanSlot)new RetainedProductMediaSlot(image.Id)).ToArray()));
        Assert.True(updated.IsSuccess);
        Assert.Equal(
            ProductErrors.StaleVersion,
            (await first.PublishAsync(new PublishProductCommand(
                updated.Value.Id,
                staleVersion))).Error);

        var publishes = await Task.WhenAll(
            first.PublishAsync(new PublishProductCommand(updated.Value.Id, updated.Value.Version)),
            second.PublishAsync(new PublishProductCommand(updated.Value.Id, updated.Value.Version)));
        var published = Assert.Single(publishes, result => result.IsSuccess);
        Assert.Equal(
            ProductErrors.PublishDraftRequired,
            Assert.Single(publishes, result => result.IsFailure).Error);

        var archives = await Task.WhenAll(
            first.ArchiveAsync(new ArchiveProductCommand(published.Value.Id, published.Value.Version)),
            second.ArchiveAsync(new ArchiveProductCommand(published.Value.Id, published.Value.Version)));
        Assert.Single(archives, result => result.IsSuccess);
        Assert.Equal(
            ProductErrors.ArchivePublishedRequired,
            Assert.Single(archives, result => result.IsFailure).Error);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(4, await db.Products
            .Where(product => product.Id == created.Value.Id)
            .Select(product => product.Version)
            .SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await db.StockMovements.CountAsync(
            movement => movement.ProductId == created.Value.Id,
            TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("publish", "brand")]
    [InlineData("publish", "universe")]
    [InlineData("archive", "brand")]
    [InlineData("archive", "universe")]
    public async Task LifecycleCommandAndReferenceArchiveRaceHasOnlySerializedOutcomes(
        string action,
        string referenceType)
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(
            factory,
            $"lifecycle-{action}-{referenceType}-race");
        var harness = CreateHarness(factory);
        var created = await harness.CreateAsync(Command(
            $"lifecycle-{action}-{referenceType}-race",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);
        var lifecycle = created.Value;
        if (action == "archive")
        {
            var published = await harness.PublishAsync(
                new PublishProductCommand(lifecycle.Id, lifecycle.Version));
            Assert.True(published.IsSuccess);
            lifecycle = published.Value;
        }

        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        async Task ArchiveReferenceAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            if (referenceType == "brand")
            {
                var brand = await db.Brands.SingleAsync(
                    item => item.Id == references.BrandId,
                    TestContext.Current.CancellationToken);
                await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
                brand.Archive(UtcNow.AddMinutes(1), "reference-archiver");
            }
            else
            {
                var universe = await db.Universes.SingleAsync(
                    item => item.Id == references.UniverseId,
                    TestContext.Current.CancellationToken);
                await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
                universe.Archive(UtcNow.AddMinutes(1), "reference-archiver");
            }

            await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        async Task<Result<ProductMutationResult>> RunLifecycleAsync()
        {
            await gate.Task.WaitAsync(TestContext.Current.CancellationToken);
            return action == "publish"
                ? await harness.PublishAsync(new PublishProductCommand(
                    lifecycle.Id,
                    lifecycle.Version))
                : await harness.ArchiveAsync(new ArchiveProductCommand(
                    lifecycle.Id,
                    lifecycle.Version));
        }

        var referenceArchive = ArchiveReferenceAsync();
        var lifecycleCommand = RunLifecycleAsync();
        gate.TrySetResult();
        var result = await lifecycleCommand;
        await referenceArchive;

        if (action == "archive")
        {
            Assert.True(result.IsSuccess);
        }
        else if (result.IsFailure)
        {
            Assert.Equal(
                referenceType == "brand"
                    ? ProductErrors.PublishBrandUnavailable
                    : ProductErrors.PublishUniverseUnavailable,
                result.Error);
        }

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(
            result.IsSuccess
                ? action == "publish" ? ProductStatus.Published : ProductStatus.Archived
                : ProductStatus.Draft,
            await verifyDb.Products
                .Where(product => product.Id == lifecycle.Id)
                .Select(product => product.Status)
                .SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PublishSaveFailureRollsBackLifecycleAuditAndPreservesInventoryAndMedia()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "publish-save-failure");
        var normal = CreateHarness(factory);
        var created = await normal.CreateAsync(Command(
            "publish-save-failure",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            InitialStock = 4,
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);
        var before = await LoadDurableStateAsync(factory, created.Value.Id);
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var failingStorage = new FakeStorage();
        var failing = CreateHarness(provider, failingStorage);

        await Assert.ThrowsAsync<InjectedSaveException>(() => failing.PublishAsync(
            new PublishProductCommand(created.Value.Id, created.Value.Version)));

        var after = await LoadDurableStateAsync(factory, created.Value.Id);
        Assert.Equal(ProductStatus.Draft, after.Product.Status);
        Assert.Equal(1, after.Product.Version);
        Assert.Null(after.Product.PublishedAtUtc);
        Assert.Null(after.Product.PublishedBy);
        Assert.Null(after.Product.ArchivedAtUtc);
        Assert.Equal(before.ImageStorageKeys, after.ImageStorageKeys);
        Assert.Equal(before.OnHandQuantity, after.OnHandQuantity);
        Assert.Equal(before.InventoryVersion, after.InventoryVersion);
        Assert.Equal(before.MovementCount, after.MovementCount);
        Assert.Empty(failingStorage.DeletedKeys);
    }

    [Fact]
    public async Task ArchiveSaveFailureRollsBackLifecycleAuditAndPreservesInventoryAndMedia()
    {
        await using var factory = await StartAndResetAsync();
        var references = await SeedReferencesAsync(factory, "archive-save-failure");
        var normal = CreateHarness(factory);
        var created = await normal.CreateAsync(Command(
            "archive-save-failure",
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            InitialStock = 7,
            Images = [new UploadProductMediaSlot(Upload())],
        });
        var published = await normal.PublishAsync(
            new PublishProductCommand(created.Value.Id, created.Value.Version));
        Assert.True(published.IsSuccess);
        var before = await LoadDurableStateAsync(factory, created.Value.Id);
        await using var provider = CreateProvider(new SaveFailureInterceptor());
        var failingStorage = new FakeStorage();
        var failing = CreateHarness(provider, failingStorage);

        await Assert.ThrowsAsync<InjectedSaveException>(() => failing.ArchiveAsync(
            new ArchiveProductCommand(published.Value.Id, published.Value.Version)));

        var after = await LoadDurableStateAsync(factory, created.Value.Id);
        Assert.Equal(ProductStatus.Published, after.Product.Status);
        Assert.Equal(2, after.Product.Version);
        Assert.Equal(before.Product.PublishedAtUtc, after.Product.PublishedAtUtc);
        Assert.Equal(before.Product.PublishedBy, after.Product.PublishedBy);
        Assert.Null(after.Product.ArchivedAtUtc);
        Assert.Null(after.Product.ArchivedBy);
        Assert.Equal(before.ImageStorageKeys, after.ImageStorageKeys);
        Assert.Equal(before.OnHandQuantity, after.OnHandQuantity);
        Assert.Equal(before.InventoryVersion, after.InventoryVersion);
        Assert.Equal(before.MovementCount, after.MovementCount);
        Assert.Empty(failingStorage.DeletedKeys);
    }

    [Theory]
    [InlineData("publish", false, false, "Authorization.Unauthorized")]
    [InlineData("publish", true, false, "Authorization.Forbidden")]
    [InlineData("archive", false, false, "Authorization.Unauthorized")]
    [InlineData("archive", true, false, "Authorization.Forbidden")]
    public async Task DeniedLifecycleCommandLeavesDurableProductInventoryAndMediaUnchanged(
        string action,
        bool authenticated,
        bool authorized,
        string expectedCode)
    {
        await using var factory = await StartAndResetAsync();
        var deniedSuffix = $"denied-{action}-{authenticated}".ToLowerInvariant();
        var references = await SeedReferencesAsync(
            factory,
            deniedSuffix);
        var harness = CreateHarness(factory);
        var created = await harness.CreateAsync(Command(
            deniedSuffix,
            references.BrandId,
            references.UniverseId,
            references.CharacterId) with
        {
            InitialStock = 3,
            Images = [new UploadProductMediaSlot(Upload())],
        });
        Assert.True(created.IsSuccess);
        var lifecycle = created.Value;
        if (action == "archive")
        {
            var published = await harness.PublishAsync(
                new PublishProductCommand(lifecycle.Id, lifecycle.Version));
            Assert.True(published.IsSuccess);
            lifecycle = published.Value;
        }

        var before = await LoadDurableStateAsync(factory, lifecycle.Id);
        Result<ProductMutationResult> denied;
        var authorization = new ConfigurableAuthorization(authenticated, authorized);
        if (action == "publish")
        {
            var command = new PublishProductCommand(lifecycle.Id, lifecycle.Version);
            var behavior = new AuthorizationBehavior<
                PublishProductCommand,
                Result<ProductMutationResult>>(authorization);
            denied = await behavior.Handle(
                command,
                token => harness.PublishHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }
        else
        {
            var command = new ArchiveProductCommand(lifecycle.Id, lifecycle.Version);
            var behavior = new AuthorizationBehavior<
                ArchiveProductCommand,
                Result<ProductMutationResult>>(authorization);
            denied = await behavior.Handle(
                command,
                token => harness.ArchiveHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        Assert.Equal(expectedCode, denied.Error.Code);
        var after = await LoadDurableStateAsync(factory, lifecycle.Id);
        Assert.Equal(before.Product.Status, after.Product.Status);
        Assert.Equal(before.Product.Version, after.Product.Version);
        Assert.Equal(before.Product.PublishedAtUtc, after.Product.PublishedAtUtc);
        Assert.Equal(before.Product.PublishedBy, after.Product.PublishedBy);
        Assert.Equal(before.Product.ArchivedAtUtc, after.Product.ArchivedAtUtc);
        Assert.Equal(before.Product.ArchivedBy, after.Product.ArchivedBy);
        Assert.Equal(before.ImageStorageKeys, after.ImageStorageKeys);
        Assert.Equal(before.OnHandQuantity, after.OnHandQuantity);
        Assert.Equal(before.InventoryVersion, after.InventoryVersion);
        Assert.Equal(before.MovementCount, after.MovementCount);
    }

    private static async Task<DurableProductState> LoadDurableStateAsync(
        ToyStoreWebApplicationFactory factory,
        Guid productId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var product = await db.Products.AsNoTracking()
            .Include(item => item.Images)
            .SingleAsync(item => item.Id == productId, TestContext.Current.CancellationToken);
        var inventory = await db.InventoryItems.AsNoTracking().SingleAsync(
            item => item.ProductId == productId,
            TestContext.Current.CancellationToken);
        var movementCount = await db.StockMovements.CountAsync(
            item => item.ProductId == productId,
            TestContext.Current.CancellationToken);
        return new DurableProductState(
            product,
            product.Images.OrderBy(image => image.SortOrder)
                .Select(image => image.StorageKey)
                .ToArray(),
            inventory.OnHandQuantity,
            inventory.Version,
            movementCount);
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

    private static Harness CreateHarness(ToyStoreWebApplicationFactory factory)
        => CreateHarness(factory.Services, new FakeStorage());

    private static Harness CreateHarness(
        IServiceProvider services,
        FakeStorage storage)
    {
        var coordinator = new ProductMediaMutationCoordinator(
            storage,
            new AlwaysUnreferencedVerifier(),
            new NoOpCleanupRegistry(),
            NullLogger<ProductMediaMutationCoordinator>.Instance);
        var sessionFactory = services.GetRequiredService<IProductMutationSessionFactory>();
        var commitResolver = new CatalogCommitOutcomeResolver(
            NullLogger<CatalogCommitOutcomeResolver>.Instance);
        return new Harness(
            new CreateInStockProductHandler(sessionFactory, coordinator, new FixedTimeProvider()),
            new UpdateDraftInStockProductHandler(sessionFactory, coordinator, new FixedTimeProvider()),
            new CreatePreOrderProductHandler(sessionFactory, coordinator, new FixedTimeProvider()),
            new UpdateDraftPreOrderProductHandler(sessionFactory, coordinator, new FixedTimeProvider()),
            new PublishProductHandler(sessionFactory, commitResolver, new FixedTimeProvider()),
            new ArchiveProductHandler(sessionFactory, commitResolver, new FixedTimeProvider()),
            storage);
    }

    private static CreateInStockProductCommand Command(
        string suffix,
        Guid brandId,
        Guid universeId,
        Guid characterId) => new(
            $"สินค้า {suffix}",
            $"Product {suffix}",
            "รายละเอียด",
            CatalogSeedIds.GundamCategory,
            brandId,
            universeId,
            [characterId],
            100,
            0,
        []);

    private static CreatePreOrderProductCommand PreOrderCommand(
        string suffix,
        ReferenceIds references,
        IReadOnlyList<ProductMediaPlanSlot> images) => new(
            $"พรีออเดอร์ {suffix}", $"Preorder {suffix}", "รายละเอียด",
            CatalogSeedIds.ArtToyCategory, references.BrandId, references.UniverseId,
            [references.CharacterId], 2000, 500, new DateOnly(2026, 12, 1),
            12, 2026, 10, 2, 7, images);

    private static async Task<ReferenceIds> SeedReferencesAsync(
        ToyStoreWebApplicationFactory factory,
        string suffix,
        bool includeMedia = true,
        bool? brandMedia = null,
        bool? universeMedia = null)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var brand = brandMedia ?? includeMedia
            ? Brand.CreateWithImage(
                Guid.NewGuid(),
                $"แบรนด์ {suffix}",
                $"Brand {suffix}",
                CatalogSlug.Create($"brand-{suffix}"),
                Media($"brand-{suffix}"),
                UtcNow,
                "test")
            : Brand.Create(
                Guid.NewGuid(),
                $"แบรนด์ {suffix}",
                $"Brand {suffix}",
                CatalogSlug.Create($"brand-{suffix}"),
                UtcNow,
                "test");
        var universe = universeMedia ?? includeMedia
            ? Universe.CreateWithLogo(
                Guid.NewGuid(),
                $"จักรวาล {suffix}",
                $"Universe {suffix}",
                CatalogSlug.Create($"universe-{suffix}"),
                Media($"universe-{suffix}"),
                UtcNow,
                "test")
            : Universe.Create(
                Guid.NewGuid(),
                $"จักรวาล {suffix}",
                $"Universe {suffix}",
                CatalogSlug.Create($"universe-{suffix}"),
                UtcNow,
                "test");
        var character = Character.Create(Guid.NewGuid(), universe.Id, $"Character {suffix}");
        db.AddRange(brand, universe, character);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new ReferenceIds(brand.Id, universe.Id, character.Id);
    }

    private static CatalogMediaReference Media(string key) =>
        CatalogMediaReference.Create(key, $"/media/{key}.webp", "รูปทดสอบ");

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private sealed record ReferenceIds(Guid BrandId, Guid UniverseId, Guid CharacterId);

    private sealed record DurableProductState(
        Product Product,
        IReadOnlyList<string> ImageStorageKeys,
        int OnHandQuantity,
        long InventoryVersion,
        int MovementCount);

    private sealed record Harness(
        CreateInStockProductHandler CreateHandler,
        UpdateDraftInStockProductHandler UpdateHandler,
        CreatePreOrderProductHandler CreatePreOrderHandler,
        UpdateDraftPreOrderProductHandler UpdatePreOrderHandler,
        PublishProductHandler PublishHandler,
        ArchiveProductHandler ArchiveHandler,
        FakeStorage Storage)
    {
        public Task<Result<ProductMutationResult>> CreateAsync(CreateInStockProductCommand command)
        {
            var behavior = new AuthorizationBehavior<
                CreateInStockProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization());
            return behavior.Handle(
                command,
                token => CreateHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        public Task<Result<ProductMutationResult>> UpdateAsync(UpdateDraftInStockProductCommand command)
        {
            var behavior = new AuthorizationBehavior<
                UpdateDraftInStockProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization());
            return behavior.Handle(
                command,
                token => UpdateHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        public Task<Result<ProductMutationResult>> CreatePreOrderAsync(CreatePreOrderProductCommand command)
        {
            var behavior = new AuthorizationBehavior<CreatePreOrderProductCommand, Result<ProductMutationResult>>(
                new StubAuthorization());
            return behavior.Handle(command, token => CreatePreOrderHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        public Task<Result<ProductMutationResult>> UpdatePreOrderAsync(UpdateDraftPreOrderProductCommand command)
        {
            var behavior = new AuthorizationBehavior<UpdateDraftPreOrderProductCommand, Result<ProductMutationResult>>(
                new StubAuthorization());
            return behavior.Handle(command, token => UpdatePreOrderHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        public Task<Result<ProductMutationResult>> PublishAsync(PublishProductCommand command)
        {
            var behavior = new AuthorizationBehavior<
                PublishProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization());
            return behavior.Handle(
                command,
                token => PublishHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }

        public Task<Result<ProductMutationResult>> ArchiveAsync(ArchiveProductCommand command)
        {
            var behavior = new AuthorizationBehavior<
                ArchiveProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization());
            return behavior.Handle(
                command,
                token => ArchiveHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeStorage : IFileStorage
    {
        private const string Batch = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private int next;

        public List<string> DeletedKeys { get; } = [];
        public int CommitCount { get; private set; }

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            var media = uploads.Select(_ =>
            {
                var suffix = (++next).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
                var key = $"{Batch}/{suffix}.webp";
                return new StagedMedia(Batch, key, $"/media/{key}", "image/webp", 10);
            }).ToArray();
            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(Batch, media)));
        }

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task DeleteCommittedAsync(
            IReadOnlyCollection<string> storageKeys,
            CancellationToken cancellationToken)
        {
            DeletedKeys.AddRange(storageKeys);
            return Task.CompletedTask;
        }

        public Task<StoredMediaRead?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken) => Task.FromResult<StoredMediaRead?>(null);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AlwaysUnreferencedVerifier : IMediaReferenceVerifier
    {
        public Task<MediaReferenceVerification> VerifyAsync(
            TrustedMediaStorageKey storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(MediaReferenceVerification.Unreferenced);
    }

    private sealed class NoOpCleanupRegistry : IMediaCleanupRegistry
    {
        public Task RecordAsync(
            MediaCleanupRegistration registration,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow.AddMinutes(2);
    }

    private sealed class StubAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policy,
            CancellationToken cancellationToken) => Task.FromResult(
                new CurrentUserAuthorizationResult(true, true, "admin-1"));
    }

    private sealed class ConfigurableAuthorization(bool authenticated, bool authorized)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policy,
            CancellationToken cancellationToken) => Task.FromResult(
                new CurrentUserAuthorizationResult(
                    authenticated,
                    authorized,
                    authorized ? "admin-1" : null));
    }

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
