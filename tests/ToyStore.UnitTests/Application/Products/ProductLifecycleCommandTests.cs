using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Application.Products.ArchiveProduct;
using ToyStore.Application.Products.PublishProduct;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application.Products;

public sealed class ProductLifecycleCommandTests
{
    [Fact]
    public async Task ValidatorsRequireProductIdentityAndPositiveVersionWithThaiMessages()
    {
        var publish = await new PublishProductValidator().ValidateAsync(
            new PublishProductCommand(Guid.Empty, 0),
            TestContext.Current.CancellationToken);
        var archive = await new ArchiveProductValidator().ValidateAsync(
            new ArchiveProductCommand(Guid.Empty, 0),
            TestContext.Current.CancellationToken);

        Assert.Contains(publish.Errors, failure =>
            failure.PropertyName == nameof(PublishProductCommand.Id)
            && failure.ErrorMessage == "รหัสสินค้าไม่ถูกต้อง");
        Assert.Contains(publish.Errors, failure =>
            failure.PropertyName == nameof(PublishProductCommand.ExpectedVersion)
            && failure.ErrorMessage == "เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
        Assert.Contains(archive.Errors, failure =>
            failure.PropertyName == nameof(ArchiveProductCommand.Id)
            && failure.ErrorMessage == "รหัสสินค้าไม่ถูกต้อง");
        Assert.Contains(archive.Errors, failure =>
            failure.PropertyName == nameof(ArchiveProductCommand.ExpectedVersion)
            && failure.ErrorMessage == "เวอร์ชันข้อมูลสินค้าไม่ถูกต้อง");
    }

    [Fact]
    public async Task PublishLocksAuthoritativeReferencesAndAdvancesLifecycleOnce()
    {
        var product = CreateDraft();
        var harness = new Harness(product);

        var result = await harness.PublishAsync(
            new PublishProductCommand(product.Id, product.Version),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.Equal(2, product.Version);
        Assert.Equal("admin-1", product.PublishedBy);
        Assert.Equal(CountingTimeProvider.FixedUtcNow, product.PublishedAtUtc);
        Assert.Equal(
            ["namespace", "product", "references"],
            harness.Session.Events);
        Assert.Equal(product.ProductCategoryId, harness.Session.LockedCategoryId);
        Assert.Equal(product.BrandId, harness.Session.LockedBrandId);
        Assert.Equal(product.UniverseId, harness.Session.LockedUniverseId);
        Assert.Equal(
            product.Characters.Select(link => link.CharacterId),
            harness.Session.LockedCharacterIds);
    }

    [Theory]
    [InlineData("brand-status", "Product.PublishBrandUnavailable")]
    [InlineData("brand-image", "Product.PublishBrandUnavailable")]
    [InlineData("universe-status", "Product.PublishUniverseUnavailable")]
    [InlineData("universe-logo", "Product.PublishUniverseUnavailable")]
    public async Task PublishRequiresActiveMediaReadyReferences(
        string scenario,
        string expectedCode)
    {
        var product = CreateDraft();
        var harness = new Harness(product);
        harness.Session.Readiness = Harness.Readiness(
            brandActive: scenario != "brand-status",
            brandImage: scenario != "brand-image",
            universeActive: scenario != "universe-status",
            universeLogo: scenario != "universe-logo");

        var result = await harness.PublishAsync(
            new PublishProductCommand(product.Id, product.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(1, product.Version);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task PublishWithoutProductImageReturnsTypedFailureBeforeReferenceLock()
    {
        var product = CreateDraft(withImage: false);
        var harness = new Harness(product);

        var result = await harness.PublishAsync(
            new PublishProductCommand(product.Id, product.Version),
            TestContext.Current.CancellationToken);

        Assert.Equal(ProductErrors.PublishRequiresImage, result.Error);
        Assert.DoesNotContain("references", harness.Session.Events);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Theory]
    [InlineData("missing", "Product.NotFound")]
    [InlineData("stale", "Product.StaleVersion")]
    [InlineData("published", "Product.PublishDraftRequired")]
    public async Task PublishReturnsTypedTargetAndLifecycleFailures(
        string scenario,
        string expectedCode)
    {
        Product? product = scenario switch
        {
            "missing" => null,
            "preorder" => CreatePreOrder(),
            _ => CreateDraft(),
        };
        if (scenario == "published")
        {
            product!.Publish(product.Version, UtcNow.AddMinutes(1), "publisher");
        }

        var harness = new Harness(product);
        var command = new PublishProductCommand(
            product?.Id ?? Guid.NewGuid(),
            scenario == "stale" ? product!.Version + 1 : product?.Version ?? 1);

        var result = await harness.PublishAsync(
            command,
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task PublishPreOrderCreatesInitialCapacityInSameMutation()
    {
        var product = CreatePreOrder();
        var harness = new Harness(product);

        var result = await harness.PublishAsync(
            new PublishProductCommand(product.Id, product.Version),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Published, product.Status);
        Assert.NotNull(harness.Session.AddedPreOrderCapacity);
        Assert.Equal(product.Id, harness.Session.AddedPreOrderCapacity.Capacity.ProductId);
        Assert.Equal(product.PreOrderOffer!.TotalCapacity,
            harness.Session.AddedPreOrderCapacity.Capacity.TotalCapacity);
        Assert.Equal(PreOrderCapacityMovementType.InitialCapacity,
            harness.Session.AddedPreOrderCapacity.Movement.Type);
    }

    [Fact]
    public async Task ArchivePublishedProductPreservesMediaAndDoesNotLockReferences()
    {
        var product = CreateDraft();
        product.Publish(product.Version, UtcNow.AddMinutes(1), "publisher");
        var imageKey = product.Images[0].StorageKey;
        var harness = new Harness(product);

        var result = await harness.ArchiveAsync(
            new ArchiveProductCommand(product.Id, product.Version));

        Assert.True(result.IsSuccess);
        Assert.Equal(ProductStatus.Archived, product.Status);
        Assert.Equal(3, product.Version);
        Assert.Equal("admin-1", product.ArchivedBy);
        Assert.Equal(imageKey, product.Images[0].StorageKey);
        Assert.Equal(["namespace", "product"], harness.Session.Events);
    }

    [Theory]
    [InlineData("missing", "Product.NotFound")]
    [InlineData("stale", "Product.StaleVersion")]
    [InlineData("draft", "Product.ArchivePublishedRequired")]
    [InlineData("archived", "Product.ArchivePublishedRequired")]
    [InlineData("preorder", "Product.InStockLifecycleRequired")]
    public async Task ArchiveReturnsTypedTargetAndLifecycleFailures(
        string scenario,
        string expectedCode)
    {
        Product? product = scenario switch
        {
            "missing" => null,
            "preorder" => CreatePreOrder(),
            _ => CreateDraft(),
        };
        if (scenario is "stale" or "archived")
        {
            product!.Publish(product.Version, UtcNow.AddMinutes(1), "publisher");
        }

        if (scenario == "archived")
        {
            product!.Archive(product.Version, UtcNow.AddMinutes(2), "archiver");
        }

        var harness = new Harness(product);
        var command = new ArchiveProductCommand(
            product?.Id ?? Guid.NewGuid(),
            scenario == "stale" ? product!.Version + 1 : product?.Version ?? 1);

        var result = await harness.ArchiveAsync(command);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Theory]
    [InlineData(false, false, "Authorization.Unauthorized")]
    [InlineData(true, false, "Authorization.Forbidden")]
    public async Task AuthorizationStopsLifecycleCommandsBeforeClockAndSession(
        bool authenticated,
        bool authorized,
        string expectedCode)
    {
        var product = CreateDraft();
        var harness = new Harness(product);
        var command = new PublishProductCommand(product.Id, product.Version);
        var behavior = new AuthorizationBehavior<
            PublishProductCommand,
            Result<ProductMutationResult>>(
                new StubAuthorization(authenticated, authorized));

        var result = await behavior.Handle(
            command,
            token => harness.PublishHandler.Handle(command, token),
            TestContext.Current.CancellationToken);

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, harness.Factory.OpenCount);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task UnauthorizedArchiveStopsBeforeClockAndSession()
    {
        var product = CreateDraft();
        product.Publish(product.Version, UtcNow.AddMinutes(1), "publisher");
        var harness = new Harness(product);
        var command = new ArchiveProductCommand(product.Id, product.Version);
        var behavior = new AuthorizationBehavior<
            ArchiveProductCommand,
            Result<ProductMutationResult>>(
                new StubAuthorization(authenticated: false, authorized: false));

        var result = await behavior.Handle(
            command,
            token => harness.ArchiveHandler.Handle(command, token),
            TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(0, harness.Factory.OpenCount);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task CancellationPropagatesBeforeLifecycleMutation()
    {
        var product = CreateDraft();
        var harness = new Harness(product);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            harness.PublishAsync(
                new PublishProductCommand(product.Id, product.Version),
                cancellation.Token));

        Assert.Equal(ProductStatus.Draft, product.Status);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task UnexpectedAuditAndVersionExhaustionInvariantsPropagate()
    {
        var futureDraft = CreateDraft(createdAtUtc: UtcNow.AddMinutes(10));
        var publishHarness = new Harness(futureDraft);
        var audit = await Assert.ThrowsAsync<ProductRuleException>(() =>
            publishHarness.PublishAsync(
                new PublishProductCommand(futureDraft.Id, futureDraft.Version),
                TestContext.Current.CancellationToken));
        Assert.Equal(ProductRule.ProductAuditTimeWentBackwards, audit.Rule);

        var published = CreateDraft();
        published.Publish(published.Version, UtcNow.AddMinutes(1), "publisher");
        var versionField = typeof(Product).GetField(
            "_version",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.NotNull(versionField);
        versionField.SetValue(published, long.MaxValue);
        var archiveHarness = new Harness(published);
        var exhausted = await Assert.ThrowsAsync<ProductRuleException>(() =>
            archiveHarness.ArchiveAsync(
                new ArchiveProductCommand(published.Id, published.Version)));
        Assert.Equal(ProductRule.ProductConcurrencyVersionExhausted, exhausted.Rule);
    }

    [Fact]
    public void CommandsUseProductPolicyAndPersistenceConcurrencyMapping()
    {
        AuthorizedProductMutationRequest<Result<ProductMutationResult>>[] commands =
        [
            new PublishProductCommand(Guid.NewGuid(), 1),
            new ArchiveProductCommand(Guid.NewGuid(), 1),
        ];

        Assert.All(commands, command =>
        {
            Assert.Equal(PolicyNames.CanManageProducts, command.RequiredPolicy);
            Assert.Equal(
                ProductErrors.StaleVersion,
                command.MapPersistenceFailure(new PersistenceFailure(
                    PersistenceFailureTarget.Request,
                    PersistenceFailureKind.ConcurrencyConflict)));
        });
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
    private static readonly Guid BrandId =
        Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid UniverseId =
        Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid CharacterId =
        Guid.Parse("30000000-0000-0000-0000-000000000001");

    private static Product CreateDraft(
        bool withImage = true,
        DateTimeOffset? createdAtUtc = null) => Product.CreateInStock(
            Guid.NewGuid(),
            "สินค้า",
            "Product",
            "รายละเอียด",
            "product",
            CatalogSeedIds.GundamCategory,
            BrandId,
            UniverseId,
            InStockOffer.Create(Money.Create(100)),
            withImage
                ? [new ProductImageDefinition(
                    Guid.NewGuid(),
                    "product/main.webp",
                    "/media/product/main.webp",
                    "รูปสินค้า")]
                : [],
            [CharacterId],
            createdAtUtc ?? UtcNow,
            "creator");

    private static Product CreatePreOrder()
    {
        var offer = PreOrderOffer.Create(
            Money.Create(1000),
            Money.Create(100),
            new DateOnly(2026, 8, 1),
            EstimatedArrival.Create(12, 2026),
            10,
            2,
            UtcNow);
        return Product.CreatePreOrder(
            Guid.NewGuid(),
            "พรีออเดอร์",
            "Pre Order",
            "รายละเอียด",
            "pre-order",
            CatalogSeedIds.ArtToyCategory,
            BrandId,
            UniverseId,
            offer,
            [new ProductImageDefinition(
                Guid.NewGuid(), "pre/main.webp", "/media/pre/main.webp", "รูป")],
            [CharacterId],
            UtcNow,
            "creator");
    }

    private sealed class Harness
    {
        public Harness(Product? product)
        {
            Session = new FakeSession(product);
            Factory = new FakeFactory(Session);
            var resolver = new CatalogCommitOutcomeResolver(
                NullLogger<CatalogCommitOutcomeResolver>.Instance);
            PublishHandler = new PublishProductHandler(Factory, resolver, Clock);
            ArchiveHandler = new ArchiveProductHandler(Factory, resolver, Clock);
            Session.Readiness = Readiness();
        }

        public FakeSession Session { get; }
        public FakeFactory Factory { get; }
        public CountingTimeProvider Clock { get; } = new();
        public PublishProductHandler PublishHandler { get; }
        public ArchiveProductHandler ArchiveHandler { get; }

        public static ProductReferenceReadiness Readiness(
            bool brandActive = true,
            bool brandImage = true,
            bool universeActive = true,
            bool universeLogo = true) => new(
                CategoryIsAllowedSeed: true,
                BrandExists: true,
                BrandStatus: brandActive
                    ? CatalogReferenceStatus.Active
                    : CatalogReferenceStatus.Archived,
                BrandHasImage: brandImage,
                UniverseExists: true,
                UniverseStatus: universeActive
                    ? CatalogReferenceStatus.Active
                    : CatalogReferenceStatus.Archived,
                UniverseHasLogo: universeLogo,
                CharacterIdsAreDistinct: true,
                ExistingCharacterIds: [CharacterId]);

        public Task<Result<ProductMutationResult>> PublishAsync(
            PublishProductCommand command,
            CancellationToken cancellationToken = default)
        {
            var behavior = new AuthorizationBehavior<
                PublishProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization(true, true));
            return behavior.Handle(
                command,
                token => PublishHandler.Handle(command, token),
                cancellationToken == default
                    ? TestContext.Current.CancellationToken
                    : cancellationToken);
        }

        public Task<Result<ProductMutationResult>> ArchiveAsync(
            ArchiveProductCommand command)
        {
            var behavior = new AuthorizationBehavior<
                ArchiveProductCommand,
                Result<ProductMutationResult>>(new StubAuthorization(true, true));
            return behavior.Handle(
                command,
                token => ArchiveHandler.Handle(command, token),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeFactory(FakeSession session) : IProductMutationSessionFactory
    {
        public int OpenCount { get; private set; }

        public ValueTask<IProductMutationSession> OpenAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult<IProductMutationSession>(session);
        }

        public Task<CatalogCommitVerification<ProductMutationEvidence>> VerifyCommitAsync(
            ProductMutationEvidence evidence,
            CancellationToken cancellationToken) => Task.FromResult(
                CatalogCommitVerificationResult.Committed(evidence));
    }

    private sealed class FakeSession(Product? product) : IProductMutationSession
    {
        public List<string> Events { get; } = [];
        public ProductReferenceReadiness Readiness { get; set; } = null!;
        public Guid LockedCategoryId { get; private set; }
        public Guid LockedBrandId { get; private set; }
        public Guid LockedUniverseId { get; private set; }
        public IReadOnlyList<Guid> LockedCharacterIds { get; private set; } = [];
        public PreOrderCapacityCreation? AddedPreOrderCapacity { get; private set; }

        public Task AcquireNamespaceLockAsync(CancellationToken cancellationToken)
        {
            Events.Add("namespace");
            return Task.CompletedTask;
        }

        public Task<Product?> LockProductAsync(Guid productId, CancellationToken cancellationToken)
        {
            Events.Add("product");
            return Task.FromResult(product?.Id == productId ? product : null);
        }

        public Task<ProductReferenceReadiness> LockReferencesAsync(
            Guid productCategoryId,
            Guid brandId,
            Guid universeId,
            IReadOnlyCollection<Guid> characterIds,
            CancellationToken cancellationToken)
        {
            Events.Add("references");
            LockedCategoryId = productCategoryId;
            LockedBrandId = brandId;
            LockedUniverseId = universeId;
            LockedCharacterIds = characterIds.ToArray();
            return Task.FromResult(Readiness);
        }

        public Task<bool> DisplayNameExistsAsync(
            string normalizedDisplayName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<bool> EnglishNameExistsAsync(
            string normalizedEnglishName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CatalogSlug> AllocateSlugAsync(
            string englishName,
            Guid? excludedId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(Product productToAdd, InventoryCreation inventoryCreation) =>
            throw new NotSupportedException();

        public void Add(PreOrderCapacityCreation capacityCreation) =>
            AddedPreOrderCapacity = capacityCreation;

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            var result = await operation(cancellationToken);
            return new CatalogMutationExecution<T>(
                result,
                result.IsSuccess
                    ? CatalogCommitOutcome.Committed
                    : CatalogCommitOutcome.DefinitelyRolledBack);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class CountingTimeProvider : TimeProvider
    {
        public static DateTimeOffset FixedUtcNow =>
            ProductLifecycleCommandTests.UtcNow.AddMinutes(2);
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
