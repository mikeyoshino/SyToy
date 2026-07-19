using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Application.Products.CreateInStockProduct;
using ToyStore.Application.Products.UpdateDraftInStockProduct;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application.Products;

public sealed class InStockProductHandlerTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(12)]
    public async Task CreatePersistsProductAndExactlyOneInitialMovementWithoutEmptyStaging(
        int initialStock)
    {
        var harness = new Harness();
        var command = harness.CreateCommand(initialStock, images: []);

        var result = await harness.AuthorizeCreateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var product = Assert.IsType<Product>(harness.Session.AddedProduct);
        var inventory = Assert.IsType<InventoryCreation>(harness.Session.AddedInventory);
        Assert.Equal(initialStock, inventory.Item.OnHandQuantity);
        Assert.Equal(StockMovementType.InitialStock, inventory.InitialMovement.Type);
        Assert.Equal(initialStock, inventory.InitialMovement.QuantityDelta);
        Assert.Equal(product.Id, inventory.Item.ProductId);
        Assert.Equal("admin-1", inventory.Item.CreatedBy);
        Assert.Equal(CountingTimeProvider.FixedUtcNow, inventory.Item.CreatedAtUtc);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal(0, harness.Storage.CommitCount);
        Assert.Equal(
            ["namespace", "product", "references", "display", "english", "slug", "add"],
            harness.Session.Events);
    }

    [Fact]
    public async Task CreateStagesOrderedUploadsAndMakesFirstImagePrimary()
    {
        var harness = new Harness();
        var command = harness.CreateCommand(
            2,
            [new UploadProductMediaSlot(Upload()), new UploadProductMediaSlot(Upload())]);

        var result = await harness.AuthorizeCreateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var product = Assert.IsType<Product>(harness.Session.AddedProduct);
        Assert.Equal(2, product.Images.Count);
        Assert.True(product.Images[0].IsPrimary);
        Assert.False(product.Images[1].IsPrimary);
        Assert.Equal([0, 1], product.Images.Select(image => image.SortOrder));
        Assert.Equal(1, harness.Storage.StageCount);
        Assert.Equal(1, harness.Storage.CommitCount);
    }

    [Fact]
    public async Task CreateSnapshotsMutableMediaAndCharactersBeforeOpeningSession()
    {
        var harness = new Harness();
        var originalCharacter = harness.CharacterIds[0];
        var characters = new List<Guid> { originalCharacter };
        var images = new List<ProductMediaPlanSlot>
        {
            new UploadProductMediaSlot(Upload()),
        };
        var command = harness.CreateCommand(0, images) with { CharacterIds = characters };
        harness.Factory.OnOpen = () =>
        {
            characters.Clear();
            characters.Add(Guid.NewGuid());
            images.Clear();
        };

        var result = await harness.AuthorizeCreateAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([originalCharacter], harness.Session.LockedCharacterIds);
        Assert.Equal(originalCharacter, Assert.Single(harness.Session.AddedProduct!.Characters).CharacterId);
        Assert.Single(harness.Session.AddedProduct.Images);
        Assert.Equal(1, harness.Storage.StageCount);
    }

    [Theory]
    [InlineData("category", "Product.CategoryUnavailable")]
    [InlineData("brand", "Product.BrandUnavailable")]
    [InlineData("universe", "Product.UniverseUnavailable")]
    [InlineData("characters", "Product.CharactersUnavailable")]
    public async Task CreateReturnsTypedReferenceFailureAndDiscardsUploads(
        string unavailable,
        string expectedCode)
    {
        var harness = new Harness();
        harness.Session.Readiness = unavailable switch
        {
            "category" => harness.Readiness(category: false),
            "brand" => harness.Readiness(brandReady: false),
            "universe" => harness.Readiness(universeReady: false),
            _ => harness.Readiness(existingCharacters: []),
        };

        var result = await harness.AuthorizeCreateAsync(
            harness.CreateCommand(0, [new UploadProductMediaSlot(Upload())]),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Null(harness.Session.AddedProduct);
        Assert.Equal(1, harness.Storage.DiscardCount);
        Assert.Equal(0, harness.Storage.CommitCount);
    }

    [Fact]
    public async Task ActiveDraftReferencesDoNotRequireBrandImageOrUniverseLogo()
    {
        var harness = new Harness();
        harness.Session.Readiness = new ProductReferenceReadiness(
            CategoryIsAllowedSeed: true,
            BrandExists: true,
            BrandStatus: CatalogReferenceStatus.Active,
            BrandHasImage: false,
            UniverseExists: true,
            UniverseStatus: CatalogReferenceStatus.Active,
            UniverseHasLogo: false,
            CharacterIdsAreDistinct: true,
            ExistingCharacterIds: harness.CharacterIds);

        var result = await harness.AuthorizeCreateAsync(
            harness.CreateCommand(0, []),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
    }

    [Theory]
    [InlineData(true, false, "Product.DuplicateDisplayName")]
    [InlineData(false, true, "Product.DuplicateEnglishName")]
    public async Task CreateReturnsTypedDuplicateFailure(
        bool duplicateDisplay,
        bool duplicateEnglish,
        string expectedCode)
    {
        var harness = new Harness();
        harness.Session.DisplayNameExists = duplicateDisplay;
        harness.Session.EnglishNameExists = duplicateEnglish;

        var result = await harness.AuthorizeCreateAsync(
            harness.CreateCommand(0, []),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Null(harness.Session.AddedProduct);
    }

    [Fact]
    public async Task UnauthorizedCreateTouchesNeitherStorageClockNorSession()
    {
        var harness = new Harness();
        var command = harness.CreateCommand(1, [new UploadProductMediaSlot(Upload())]);
        var behavior = new AuthorizationBehavior<
            CreateInStockProductCommand,
            Result<ProductMutationResult>>(new StubAuthorization(false, false));

        var result = await behavior.Handle(
            command,
            cancellationToken => harness.CreateHandler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal(0, harness.Clock.CallCount);
        Assert.Equal(0, harness.Factory.OpenCount);
    }

    [Fact]
    public async Task CancellationPropagatesBeforeAnyMutation()
    {
        var harness = new Harness();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.AuthorizeCreateAsync(
                harness.CreateCommand(1, [new UploadProductMediaSlot(Upload())]),
                cancellation.Token));

        Assert.Null(harness.Session.AddedProduct);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task UpdateReplacesAllDetailsCharactersAndOrderedImagesWithoutInventoryMutation()
    {
        var existing = CreateExistingProduct();
        var originalFirst = existing.Images[0];
        var originalSecond = existing.Images[1];
        var harness = new Harness(existing);
        var command = harness.UpdateCommand(
            existing,
            [
                new RetainedProductMediaSlot(originalSecond.Id),
                new UploadProductMediaSlot(Upload()),
            ]);

        var result = await harness.AuthorizeUpdateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("สินค้าแก้ไข", existing.DisplayName);
        Assert.Equal(250, existing.InStockOffer!.Price.Amount);
        Assert.Equal(harness.SecondBrandId, existing.BrandId);
        Assert.Equal(harness.SecondUniverseId, existing.UniverseId);
        Assert.Equal(harness.SecondCharacterIds.Order(), existing.Characters.Select(x => x.CharacterId).Order());
        Assert.Equal(originalSecond.Id, existing.Images[0].Id);
        Assert.DoesNotContain(existing.Images, image => image.Id == originalFirst.Id);
        Assert.Equal(2, existing.Version);
        Assert.Null(harness.Session.AddedInventory);
        Assert.Equal(["old-1"], harness.Storage.DeletedKeys);
    }

    [Fact]
    public async Task UpdateNoOpKeepsVersionAndDoesNotStageOrDelete()
    {
        var existing = CreateExistingProduct();
        var harness = new Harness(existing);
        var command = new UpdateDraftInStockProductCommand(
            existing.Id,
            existing.Version,
            $" {existing.DisplayName} ",
            $" {existing.EnglishName} ",
            $" {existing.Description} ",
            existing.ProductCategoryId,
            existing.BrandId,
            existing.UniverseId,
            existing.Characters.Select(link => link.CharacterId).ToArray(),
            existing.InStockOffer!.Price.Amount,
            existing.Images.OrderBy(image => image.SortOrder)
                .Select(image => (ProductMediaPlanSlot)new RetainedProductMediaSlot(image.Id))
                .ToArray());

        var result = await harness.AuthorizeUpdateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, existing.Version);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Empty(harness.Storage.DeletedKeys);
    }

    [Fact]
    public async Task UpdateSnapshotsMutableMediaAndCharactersBeforeOpeningSession()
    {
        var existing = CreateExistingProduct();
        var harness = new Harness(existing);
        var originalCharacter = existing.Characters[0].CharacterId;
        var retainedId = existing.Images[0].Id;
        var characters = new List<Guid> { originalCharacter };
        var images = new List<ProductMediaPlanSlot>
        {
            new RetainedProductMediaSlot(retainedId),
        };
        var command = new UpdateDraftInStockProductCommand(
            existing.Id,
            existing.Version,
            existing.DisplayName,
            existing.EnglishName,
            existing.Description,
            existing.ProductCategoryId,
            existing.BrandId,
            existing.UniverseId,
            characters,
            existing.InStockOffer!.Price.Amount,
            images);
        harness.Factory.OnOpen = () =>
        {
            characters.Clear();
            characters.Add(Guid.NewGuid());
            images.Clear();
        };

        var result = await harness.AuthorizeUpdateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal([originalCharacter], harness.Session.LockedCharacterIds);
        Assert.Equal(retainedId, Assert.Single(existing.Images).Id);
        Assert.Equal(originalCharacter, Assert.Single(existing.Characters).CharacterId);
    }

    [Fact]
    public async Task UnexpectedAuditInvariantPropagatesInsteadOfBecomingBusinessFailure()
    {
        var existing = CreateExistingProduct();
        existing.UpdateDraftInStock(
            existing.DisplayName,
            existing.EnglishName,
            "รายละเอียดใหม่กว่า clock",
            existing.Slug,
            existing.ProductCategoryId,
            existing.BrandId,
            existing.UniverseId,
            existing.InStockOffer!,
            existing.Images.Select(image => new ProductImageDefinition(
                image.Id,
                image.StorageKey,
                image.PublicRelativeUrl,
                image.AltText)).ToArray(),
            existing.Characters.Select(link => link.CharacterId).ToArray(),
            existing.Version,
            UtcNow.AddMinutes(10),
            "newer-admin");
        var harness = new Harness(existing);

        var exception = await Assert.ThrowsAsync<ProductRuleException>(() =>
            harness.AuthorizeUpdateAsync(harness.UpdateCommand(existing, [])));

        Assert.Equal(ProductRule.ProductAuditTimeWentBackwards, exception.Rule);
    }

    [Fact]
    public async Task VersionExhaustionInvariantPropagatesInsteadOfBecomingStaleFailure()
    {
        var existing = CreateExistingProduct();
        var versionField = typeof(Product).GetField(
            "_version",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(versionField);
        versionField.SetValue(existing, long.MaxValue);
        var harness = new Harness(existing);

        var exception = await Assert.ThrowsAsync<ProductRuleException>(() =>
            harness.AuthorizeUpdateAsync(harness.UpdateCommand(existing, [])));

        Assert.Equal(ProductRule.ProductConcurrencyVersionExhausted, exception.Rule);
    }

    [Theory]
    [InlineData("missing", "Product.NotFound")]
    [InlineData("stale", "Product.StaleVersion")]
    [InlineData("archived", "Product.EditableInStockRequired")]
    public async Task UpdateReturnsTypedTargetFailuresBeforeReferences(
        string scenario,
        string expectedCode)
    {
        var existing = scenario == "missing" ? null : CreateExistingProduct();
        if (scenario == "archived")
        {
            existing!.Publish(existing.Version, UtcNow.AddMinutes(1), "publisher");
            existing.Archive(existing.Version, UtcNow.AddMinutes(2), "archiver");
        }

        var harness = new Harness(existing);
        var command = harness.UpdateCommand(existing ?? CreateExistingProduct(), []);
        if (scenario == "missing")
        {
            command = command with { Id = Guid.NewGuid() };
        }
        else if (scenario == "stale")
        {
            command = command with { ExpectedVersion = existing!.Version + 1 };
        }

        var result = await harness.AuthorizeUpdateAsync(command);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.DoesNotContain("references", harness.Session.Events);
    }

    [Fact]
    public async Task PublishedUpdateKeepsPublishedStatusAndUsesCurrentVersion()
    {
        var existing = CreateExistingProduct();
        existing.Publish(existing.Version, UtcNow.AddMinutes(1), "publisher");
        var harness = new Harness(existing);
        var command = harness.UpdateCommand(
            existing,
            existing.Images.OrderBy(image => image.SortOrder)
                .Select(image => (ProductMediaPlanSlot)new RetainedProductMediaSlot(image.Id))
                .ToArray());

        var result = await harness.AuthorizeUpdateAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Published, existing.Status);
        Assert.Equal("สินค้าแก้ไข", existing.DisplayName);
        Assert.Equal(3, existing.Version);
    }

    [Fact]
    public async Task StorageValidationFailureMapsToImagesAndDoesNotMutate()
    {
        var harness = new Harness();
        harness.Storage.StageFailure = MediaStorageErrors.InvalidSignature;

        var result = await harness.AuthorizeCreateAsync(
            harness.CreateCommand(0, [new UploadProductMediaSlot(Upload())]),
            TestContext.Current.CancellationToken);

        Assert.Equal(MediaStorageErrors.InvalidSignature, result.Error);
        var failure = Assert.Single(result.ValidationFailures);
        Assert.Equal(nameof(CreateInStockProductCommand.Images), failure.PropertyName);
        Assert.Null(harness.Session.AddedProduct);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task IndeterminateCommitWithoutAuthoritativeProofFailsClosed()
    {
        var harness = new Harness();
        harness.Session.CommitOutcome = CatalogCommitOutcome.Indeterminate;
        harness.Factory.Verification = evidence =>
            CatalogCommitVerificationResult.Unavailable<ProductMutationEvidence>();

        var result = await harness.AuthorizeCreateAsync(
            harness.CreateCommand(0, []),
            TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, result.Error);
        Assert.NotNull(harness.Session.AddedProduct);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 2, 0, 0, TimeSpan.Zero);

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private static Product CreateExistingProduct()
    {
        var universeId = Guid.Parse("10000000-0000-0000-0000-000000000001");
        return Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้าเดิม",
            "Existing Product",
            "รายละเอียดเดิม",
            "existing-product",
            CatalogSeedIds.ArtToyCategory,
            Guid.Parse("20000000-0000-0000-0000-000000000001"),
            universeId,
            InStockOffer.Create(Money.Create(100)),
            [
                new ProductImageDefinition(
                    Guid.NewGuid(), "old-1", "/media/old-1.webp", "รูปเดิม 1"),
                new ProductImageDefinition(
                    Guid.NewGuid(), "old-2", "/media/old-2.webp", "รูปเดิม 2"),
            ],
            [Guid.Parse("30000000-0000-0000-0000-000000000001")],
            UtcNow,
            "creator");
    }

    private sealed class Harness
    {
        public Harness(Product? current = null)
        {
            Session = new FakeSession(current);
            Factory = new FakeFactory(Session);
            Coordinator = new ProductMediaMutationCoordinator(
                Storage,
                new AlwaysUnreferencedVerifier(),
                new NoOpCleanupRegistry(),
                NullLogger<ProductMediaMutationCoordinator>.Instance);
            CreateHandler = new CreateInStockProductHandler(Factory, Coordinator, Clock);
            UpdateHandler = new UpdateDraftInStockProductHandler(Factory, Coordinator, Clock);
        }

        public Guid BrandId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000001");
        public Guid UniverseId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000001");
        public Guid[] CharacterIds { get; } =
            [Guid.Parse("30000000-0000-0000-0000-000000000001")];
        public Guid SecondBrandId { get; } = Guid.Parse("20000000-0000-0000-0000-000000000002");
        public Guid SecondUniverseId { get; } = Guid.Parse("10000000-0000-0000-0000-000000000002");
        public Guid[] SecondCharacterIds { get; } =
            [Guid.Parse("30000000-0000-0000-0000-000000000002")];
        public FakeStorage Storage { get; } = new();
        public CountingTimeProvider Clock { get; } = new();
        public FakeSession Session { get; }
        public FakeFactory Factory { get; }
        public ProductMediaMutationCoordinator Coordinator { get; }
        public CreateInStockProductHandler CreateHandler { get; }
        public UpdateDraftInStockProductHandler UpdateHandler { get; }

        public CreateInStockProductCommand CreateCommand(
            int initialStock,
            IReadOnlyList<ProductMediaPlanSlot> images) => new(
                "สินค้าใหม่",
                "New Product",
                "รายละเอียดสินค้า",
                CatalogSeedIds.GundamCategory,
                BrandId,
                UniverseId,
                CharacterIds,
                150,
                initialStock,
                images);

        public UpdateDraftInStockProductCommand UpdateCommand(
            Product product,
            IReadOnlyList<ProductMediaPlanSlot> images) => new(
                product.Id,
                product.Version,
                "สินค้าแก้ไข",
                "Updated Product",
                "รายละเอียดแก้ไข",
                CatalogSeedIds.GundamCategory,
                SecondBrandId,
                SecondUniverseId,
                SecondCharacterIds,
                250,
                images);

        public ProductReferenceReadiness Readiness(
            bool category = true,
            bool brandReady = true,
            bool universeReady = true,
            IReadOnlyList<Guid>? existingCharacters = null) => new(
                category,
                BrandExists: true,
                BrandStatus: brandReady ? CatalogReferenceStatus.Active : CatalogReferenceStatus.Archived,
                BrandHasImage: brandReady,
                UniverseExists: true,
                UniverseStatus: universeReady ? CatalogReferenceStatus.Active : CatalogReferenceStatus.Archived,
                UniverseHasLogo: universeReady,
                CharacterIdsAreDistinct: true,
                ExistingCharacterIds: existingCharacters ?? CharacterIds);

        public Task<Result<ProductMutationResult>> AuthorizeCreateAsync(
            CreateInStockProductCommand command,
            CancellationToken cancellationToken = default)
        {
            Session.Readiness ??= Readiness(
                existingCharacters: command.CharacterIds.ToArray());
            var behavior = new AuthorizationBehavior<
                CreateInStockProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization(true, true));
            return behavior.Handle(
                command,
                token => CreateHandler.Handle(command, token),
                cancellationToken == default
                    ? TestContext.Current.CancellationToken
                    : cancellationToken);
        }

        public Task<Result<ProductMutationResult>> AuthorizeUpdateAsync(
            UpdateDraftInStockProductCommand command)
        {
            Session.Readiness ??= Readiness(
                existingCharacters: command.CharacterIds.ToArray());
            var behavior = new AuthorizationBehavior<
                UpdateDraftInStockProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization(true, true));
            return behavior.Handle(
                command,
                token => UpdateHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeFactory(FakeSession session) : IProductMutationSessionFactory
    {
        public int OpenCount { get; private set; }
        public Action? OnOpen { get; set; }
        public Func<ProductMutationEvidence, CatalogCommitVerification<ProductMutationEvidence>>
            Verification
        { get; set; } = evidence =>
                CatalogCommitVerificationResult.Committed(evidence);

        public ValueTask<IProductMutationSession> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            OnOpen?.Invoke();
            return ValueTask.FromResult<IProductMutationSession>(session);
        }

        public Task<CatalogCommitVerification<ProductMutationEvidence>> VerifyCommitAsync(
            ProductMutationEvidence evidence,
            CancellationToken cancellationToken) => Task.FromResult(Verification(evidence));
    }

    private sealed class FakeSession(Product? current) : IProductMutationSession
    {
        private bool active;

        public List<string> Events { get; } = [];
        public ProductReferenceReadiness? Readiness { get; set; }
        public bool DisplayNameExists { get; set; }
        public bool EnglishNameExists { get; set; }
        public Product? AddedProduct { get; private set; }
        public InventoryCreation? AddedInventory { get; private set; }
        public IReadOnlyList<Guid> LockedCharacterIds { get; private set; } = [];
        public CatalogCommitOutcome CommitOutcome { get; set; } = CatalogCommitOutcome.Committed;

        public Task AcquireNamespaceLockAsync(CancellationToken cancellationToken)
        {
            Assert.True(active);
            Events.Add("namespace");
            return Task.CompletedTask;
        }

        public Task<Product?> LockProductAsync(Guid productId, CancellationToken cancellationToken)
        {
            Events.Add("product");
            return Task.FromResult(current?.Id == productId ? current : null);
        }

        public Task<ProductReferenceReadiness> LockReferencesAsync(
            Guid productCategoryId,
            Guid brandId,
            Guid universeId,
            IReadOnlyCollection<Guid> characterIds,
            CancellationToken cancellationToken)
        {
            Events.Add("references");
            LockedCharacterIds = characterIds.ToArray();
            return Task.FromResult(Readiness!);
        }

        public Task<bool> DisplayNameExistsAsync(
            string normalizedDisplayName,
            Guid? excludedId,
            CancellationToken cancellationToken)
        {
            Events.Add("display");
            return Task.FromResult(DisplayNameExists);
        }

        public Task<bool> EnglishNameExistsAsync(
            string normalizedEnglishName,
            Guid? excludedId,
            CancellationToken cancellationToken)
        {
            Events.Add("english");
            return Task.FromResult(EnglishNameExists);
        }

        public Task<CatalogSlug> AllocateSlugAsync(
            string englishName,
            Guid? excludedId,
            CancellationToken cancellationToken)
        {
            Events.Add("slug");
            return Task.FromResult(CatalogSlugGenerator.GenerateBase(englishName));
        }

        public void Add(Product product, InventoryCreation inventoryCreation)
        {
            Events.Add("add");
            AddedProduct = product;
            AddedInventory = inventoryCreation;
        }

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            active = true;
            try
            {
                var result = await operation(cancellationToken);
                return new CatalogMutationExecution<T>(
                    result,
                    result.IsSuccess
                        ? CommitOutcome
                        : CatalogCommitOutcome.DefinitelyRolledBack,
                    result.IsSuccess && CommitOutcome == CatalogCommitOutcome.Indeterminate
                        ? CatalogCommitFailure.Create(new IOException("commit acknowledgement lost"))
                        : null);
            }
            finally
            {
                active = false;
            }
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStorage : IFileStorage
    {
        private const string Batch = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private int nextMedia;

        public Error? StageFailure { get; set; }
        public int StageCount { get; private set; }
        public int CommitCount { get; private set; }
        public int DiscardCount { get; private set; }
        public List<string> DeletedKeys { get; } = [];

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            StageCount++;
            if (StageFailure is not null)
            {
                return Task.FromResult(Result<StagedMediaBatch>.Failure(StageFailure));
            }

            var media = uploads.Select(_ =>
            {
                var suffix = (++nextMedia).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
                var key = $"{Batch}/{suffix}.webp";
                var thumbnailSuffix = (++nextMedia).ToString("x32", System.Globalization.CultureInfo.InvariantCulture);
                var thumbnailKey = $"{Batch}/{thumbnailSuffix}.webp";
                return new StagedMedia(Batch, key, $"/media/{key}", "image/webp", 10,
                    thumbnailKey, $"/media/{thumbnailKey}", 5);
            }).ToArray();
            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(Batch, media)));
        }

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
        {
            CommitCount++;
            return Task.CompletedTask;
        }

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken)
        {
            DiscardCount++;
            return Task.CompletedTask;
        }

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

    private sealed class CountingTimeProvider : TimeProvider
    {
        public static DateTimeOffset FixedUtcNow =>
            InStockProductHandlerTests.UtcNow.AddMinutes(2);
        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return FixedUtcNow;
        }
    }

    private sealed class StubAuthorization(bool authenticated, bool authorized)
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
}
