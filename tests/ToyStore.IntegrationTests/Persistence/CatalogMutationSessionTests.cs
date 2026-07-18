using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.CreateBrand;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure;
using ToyStore.Infrastructure.Persistence;
using ToyStore.IntegrationTests.Infrastructure;

namespace ToyStore.IntegrationTests.Persistence;

[Collection(PostgreSqlTestGroup.Name)]
public sealed class CatalogMutationSessionTests(PostgreSqlFixture postgreSql)
{
    [Fact]
    public async Task CreateCharacterCommitAcknowledgementLossUsesFreshExactVerificationOnce()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new CommitAcknowledgementFailureInterceptor(
            new InjectedCommitAcknowledgementException());
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(),
            interceptor);
        await using var persistenceProvider = CreateProvider(contextFactory);
        var countingFactory = new CountingCharacterSessionFactory(
            persistenceProvider.GetRequiredService<ICharacterMutationSessionFactory>());
        var handler = new CreateCharacterHandler(
            countingFactory,
            new CatalogCommitOutcomeResolver(
                NullLogger<CatalogCommitOutcomeResolver>.Instance));
        var command = new CreateCharacterCommand(
            CatalogSeedIds.MarvelUniverse,
            "Commit Evidence Character");
        var authorization = new AuthorizationBehavior<
            CreateCharacterCommand,
            Result<CharacterOption>>(new AdminAuthorization());

        var result = await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, countingFactory.CallbackCount);
        Assert.Equal(1, countingFactory.VerificationCount);
        Assert.Equal(1, interceptor.InvocationCount);
        Assert.Equal(2, contextFactory.CreatedContexts.Length);
        var primaryContext = contextFactory.CreatedContexts[0];
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            primaryContext.Characters.CountAsync(TestContext.Current.CancellationToken));
        await using var scope = applicationFactory.Services.CreateAsyncScope();
        var persisted = await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Characters
            .AsNoTracking()
            .SingleAsync(
                character => character.Id == result.Value.Id,
                TestContext.Current.CancellationToken);
        Assert.Equal(result.Value.Name, persisted.Name);
        Assert.Equal(result.Value.UniverseId, persisted.UniverseId);
    }

    [Fact]
    public async Task CreateBrandCommitAcknowledgementLossRetainsReferencedLocalMediaWithoutLedgerEntry()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new CommitAcknowledgementFailureInterceptor(
            new InjectedCommitAcknowledgementException());
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(),
            interceptor);
        await using var persistenceProvider = CreateProvider(contextFactory);
        await using var scope = applicationFactory.Services.CreateAsyncScope();
        var handler = new CreateBrandHandler(
            persistenceProvider.GetRequiredService<IBrandMutationSessionFactory>(),
            scope.ServiceProvider.GetRequiredService<MediaMutationCoordinator>(),
            scope.ServiceProvider.GetRequiredService<TimeProvider>());
        var command = new CreateBrandCommand(
            "แบรนด์ยืนยันผลคอมมิต",
            "Commit Evidence Brand",
            new MediaUpload(
                new MemoryStream([0xff, 0xd8, 0xff, 1, 2, 3]),
                "image/jpeg"));
        var authorization = new AuthorizationBehavior<
            CreateBrandCommand,
            Result<BrandMutationResult>>(new AdminAuthorization());

        var result = await authorization.Handle(
            command,
            cancellationToken => handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, interceptor.InvocationCount);
        Assert.Equal(2, contextFactory.CreatedContexts.Length);
        var persisted = await scope.ServiceProvider
            .GetRequiredService<ApplicationDbContext>()
            .Brands
            .AsNoTracking()
            .SingleAsync(
                brand => brand.Id == result.Value.Id,
                TestContext.Current.CancellationToken);
        Assert.NotNull(persisted.Image);
        Assert.True(File.Exists(CommittedPath(
            applicationFactory,
            persisted.Image.StorageKey)));
        Assert.Empty(StagingEntries(applicationFactory));
        Assert.Equal(
            0L,
            await CountUnresolvedCleanupEntriesAsync(persisted.Image.StorageKey));
    }

    [Fact]
    public async Task SaveFailureRollsBackAndLeavesNoBrandRow()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new SaveFailureInterceptor();
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(), interceptor);
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("save-failure");
        var callbackCount = 0;

        await Assert.ThrowsAsync<InjectedSaveException>(() => session.ExecuteOnceAsync<string>(
            _ =>
            {
                callbackCount++;
                session.Add(brand);
                return Task.FromResult(Result<string>.Success("created"));
            },
            TestContext.Current.CancellationToken));

        Assert.Equal(1, callbackCount);
        var primaryContext = Assert.Single(contextFactory.CreatedContexts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => primaryContext.Brands.CountAsync(
            TestContext.Current.CancellationToken));
        await using var verification = CreateDbContext();
        Assert.False(await verification.Brands.AnyAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SaveFailureReleasesSingleConnectionBeforeMediaCompensationVerification()
    {
        await using var applicationFactory = await ResetAsync();
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(), new SaveFailureInterceptor());
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var storage = new RecordingFileStorage();
        var coordinator = new MediaMutationCoordinator(
            storage,
            provider.GetRequiredService<IMediaReferenceVerifier>(),
            new NoOpCleanupRegistry(),
            NullLogger<MediaMutationCoordinator>.Instance);
        var brandId = Guid.NewGuid();

        await Assert.ThrowsAsync<InjectedSaveException>(() => coordinator.ExecuteAsync(
            new MediaUpload(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg"),
            session,
            (media, _) =>
            {
                session.Add(Brand.CreateWithImage(
                    brandId,
                    "แบรนด์ชดเชย",
                    "Compensation Brand",
                    CatalogSlug.Create("compensation-brand"),
                    CatalogMediaReference.Create(
                        media.StorageKey,
                        media.PublicRelativeUrl,
                        "รูปแบรนด์ชดเชย"),
                    new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
                    "test"));
                return Task.FromResult(Result<Guid>.Success(brandId));
            },
            _ => Task.FromResult(CatalogCommitVerificationResult.NotCommitted<Guid>()),
            new MediaMutationContext("Brand", brandId, null),
            TestContext.Current.CancellationToken));

        Assert.Equal(1, storage.CommitCount);
        Assert.Equal([storage.StorageKey], storage.DeletedKeys);
        Assert.Single(storage.DeleteTokens);
        Assert.All(storage.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task CommitAcknowledgementFailureReleasesPrimaryContextBeforeFreshVerification()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new CommitAcknowledgementFailureInterceptor(
            new InjectedCommitAcknowledgementException());
        var contextFactory = new RecordingContextFactory(ConstrainedConnectionString(), interceptor);
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("ack-lost");
        var evidence = BrandMutationEvidence.Capture(brand);

        var execution = await session.ExecuteOnceAsync(
            _ =>
            {
                session.Add(brand);
                return Task.FromResult(Result<BrandMutationEvidence>.Success(evidence));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.Indeterminate, execution.CommitOutcome);
        Assert.NotNull(execution.CommitFailure);
        Assert.IsType<InjectedCommitAcknowledgementException>(
            execution.CommitFailure.OriginalException);
        var primaryContext = Assert.Single(contextFactory.CreatedContexts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => primaryContext.Brands.CountAsync(
            TestContext.Current.CancellationToken));

        var verification = await factory.VerifyCommitAsync(
            evidence,
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitVerification.Committed, verification.Outcome);
        Assert.Equal(brand.Id, verification.AuthoritativeState.Id);
        Assert.Equal(2, contextFactory.CreatedContexts.Length);
    }

    [Fact]
    public async Task CommittedOutcomeReleasesPrimaryContextBeforeFreshVerification()
    {
        await using var applicationFactory = await ResetAsync();
        var contextFactory = new RecordingContextFactory(ConstrainedConnectionString());
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("committed-release");
        var evidence = BrandMutationEvidence.Capture(brand);

        var execution = await session.ExecuteOnceAsync(
            _ =>
            {
                session.Add(brand);
                return Task.FromResult(Result<Guid>.Success(brand.Id));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.Committed, execution.CommitOutcome);
        var primaryContext = Assert.Single(contextFactory.CreatedContexts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => primaryContext.Brands.CountAsync(
            TestContext.Current.CancellationToken));
        var verification = await factory.VerifyCommitAsync(
            evidence,
            TestContext.Current.CancellationToken);
        Assert.Equal(CatalogCommitVerification.Committed, verification.Outcome);
    }

    [Fact]
    public async Task CancellationAfterDurableCommitIsPreservedAsIndeterminateEvidence()
    {
        await using var applicationFactory = await ResetAsync();
        var cancellation = new OperationCanceledException("injected commit cancellation");
        await using var provider = CreateProvider(
            new CommitAcknowledgementFailureInterceptor(cancellation));
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("commit-cancellation");

        var execution = await session.ExecuteOnceAsync(
            _ =>
            {
                session.Add(brand);
                return Task.FromResult(Result<Guid>.Success(brand.Id));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.Indeterminate, execution.CommitOutcome);
        Assert.NotNull(execution.CommitFailure);
        Assert.Same(cancellation, execution.CommitFailure.OriginalException);
        Assert.True(execution.CommitFailure.IsCancellation);
        await using var verification = CreateDbContext();
        Assert.True(await verification.Brands.AnyAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TypedFailureRollbackUsesNonCancellableTokenAndLeavesNoRow()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new RollbackRecordingInterceptor();
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(), interceptor);
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("typed-rollback");

        var execution = await session.ExecuteOnceAsync<string>(
            _ =>
            {
                session.Add(brand);
                return Task.FromResult(Result<string>.Failure(new Error(
                    "Test.TypedFailure",
                    "ข้อมูลไม่ถูกต้อง",
                    ErrorType.Conflict)));
            },
            TestContext.Current.CancellationToken);

        Assert.Equal(CatalogCommitOutcome.DefinitelyRolledBack, execution.CommitOutcome);
        Assert.False(interceptor.RollbackToken.CanBeCanceled);
        var primaryContext = Assert.Single(contextFactory.CreatedContexts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => primaryContext.Brands.CountAsync(
            TestContext.Current.CancellationToken));
        await using var verification = CreateDbContext();
        Assert.False(await verification.Brands.AnyAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task RollbackFailureStillReleasesPrimaryContextAndPreservesTheFault()
    {
        await using var applicationFactory = await ResetAsync();
        var interceptor = new RollbackFailureInterceptor();
        var contextFactory = new RecordingContextFactory(
            ConstrainedConnectionString(), interceptor);
        await using var provider = CreateProvider(contextFactory);
        var factory = provider.GetRequiredService<IBrandMutationSessionFactory>();
        await using var session = await factory.OpenAsync(TestContext.Current.CancellationToken);
        var brand = CreateBrand("rollback-failure");

        await Assert.ThrowsAsync<InjectedRollbackException>(() => session.ExecuteOnceAsync<string>(
            _ =>
            {
                session.Add(brand);
                return Task.FromResult(Result<string>.Failure(new Error(
                    "Test.RollbackFailure",
                    "ย้อนกลับไม่สำเร็จ",
                    ErrorType.Conflict)));
            },
            TestContext.Current.CancellationToken));

        Assert.False(interceptor.RollbackToken.CanBeCanceled);
        var primaryContext = Assert.Single(contextFactory.CreatedContexts);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => primaryContext.Brands.CountAsync(
            TestContext.Current.CancellationToken));
        await using var verification = CreateDbContext();
        Assert.False(await verification.Brands.AnyAsync(
            value => value.Id == brand.Id,
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task MediaReferenceVerifierChecksBrandUniverseAndProductImageTables()
    {
        await using var applicationFactory = await ResetAsync();
        await using var scope = applicationFactory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var brandMedia = Media("references/brand");
        var universeMedia = Media("references/universe");
        var productMedia = Media("references/product");
        var brand = Brand.CreateWithImage(
            Guid.NewGuid(), "แบรนด์อ้างอิง", "Reference Brand",
            CatalogSlug.Create("reference-brand"), brandMedia, now, "test");
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(), "จักรวาลอ้างอิง", "Reference Universe",
            CatalogSlug.Create("reference-universe"), universeMedia, now, "test");
        var product = Product.CreateInStock(
            Guid.NewGuid(), "สินค้าทดสอบ", "Reference Product", "รายละเอียด",
            "reference-product", CatalogSeedIds.ArtToyCategory, brand.Id, universe.Id,
            InStockOffer.Create(Money.Create(100)), now, "test");
        product.AddImage(
            Guid.NewGuid(), productMedia.StorageKey, productMedia.PublicRelativeUrl,
            productMedia.AltText, now, "test");
        dbContext.AddRange(brand, universe, product);
        await dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        var verifier = scope.ServiceProvider.GetRequiredService<IMediaReferenceVerifier>();

        Assert.Equal(
            MediaReferenceVerification.Referenced,
            await verifier.VerifyAsync(
                TrustedMediaStorageKey.From(brandMedia), TestContext.Current.CancellationToken));
        Assert.Equal(
            MediaReferenceVerification.Referenced,
            await verifier.VerifyAsync(
                TrustedMediaStorageKey.From(universeMedia), TestContext.Current.CancellationToken));
        Assert.Equal(
            MediaReferenceVerification.Referenced,
            await verifier.VerifyAsync(
                TrustedMediaStorageKey.From(productMedia), TestContext.Current.CancellationToken));
        Assert.Equal(
            MediaReferenceVerification.Unreferenced,
            await verifier.VerifyAsync(
                TrustedMediaStorageKey.From(Media("references/missing")),
                TestContext.Current.CancellationToken));
    }

    private async Task<ToyStoreWebApplicationFactory> ResetAsync()
    {
        var factory = new ToyStoreWebApplicationFactory(postgreSql.ConnectionString);
        using var client = factory.CreateClient();
        await postgreSql.ResetAsync(factory.Services);
        return factory;
    }

    private ServiceProvider CreateProvider(params IInterceptor[] interceptors) =>
        CreateProvider(new RecordingContextFactory(ConstrainedConnectionString(), interceptors));

    private static ServiceProvider CreateProvider(
        RecordingContextFactory contextFactory)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Database"] = contextFactory.ConnectionString,
            })
            .Build());
        services.RemoveAll<IDbContextFactory<ApplicationDbContext>>();
        services.AddSingleton<IDbContextFactory<ApplicationDbContext>>(contextFactory);
        return services.BuildServiceProvider();
    }

    private ApplicationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(postgreSql.ConnectionString)
            .Options);

    private async Task<long> CountUnresolvedCleanupEntriesAsync(string storageKey)
    {
        await using var dbContext = CreateDbContext();
        await dbContext.Database.OpenConnectionAsync(TestContext.Current.CancellationToken);
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM "MediaCleanupEntries"
            WHERE "StorageKey" = @storageKey AND "ResolvedAtUtc" IS NULL;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "storageKey";
        parameter.Value = storageKey;
        command.Parameters.Add(parameter);
        var count = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return Convert.ToInt64(count, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static string CommittedPath(
        ToyStoreWebApplicationFactory factory,
        string storageKey) =>
        Path.Combine(
            factory.StorageRootPath,
            "files",
            storageKey.Replace('/', Path.DirectorySeparatorChar));

    private static string[] StagingEntries(ToyStoreWebApplicationFactory factory)
    {
        var stagingRoot = Path.Combine(factory.StorageRootPath, ".staging");
        return Directory.Exists(stagingRoot)
            ? Directory.GetFileSystemEntries(
                stagingRoot,
                "*",
                SearchOption.AllDirectories)
            : [];
    }

    private string ConstrainedConnectionString() =>
        $"{postgreSql.ConnectionString};Maximum Pool Size=1;Timeout=5";

    private static Brand CreateBrand(string suffix) =>
        Brand.Create(
            Guid.NewGuid(), $"แบรนด์ {suffix}", $"Brand {suffix}",
            CatalogSlug.Create($"brand-{suffix}"),
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            "test");

    private static CatalogMediaReference Media(string storageKey) =>
        CatalogMediaReference.Create(
            storageKey,
            $"/media/{storageKey}.webp",
            $"รูป {storageKey}");

    private sealed class RecordingContextFactory : IDbContextFactory<ApplicationDbContext>
    {
        private readonly object gate = new();
        private readonly DbContextOptions<ApplicationDbContext> options;
        private readonly List<ApplicationDbContext> contexts = [];

        internal RecordingContextFactory(string connectionString, params IInterceptor[] interceptors)
        {
            ConnectionString = connectionString;
            options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(connectionString)
                .AddInterceptors(interceptors)
                .Options;
        }

        internal string ConnectionString { get; }

        internal ApplicationDbContext[] CreatedContexts
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

    private sealed class CountingCharacterSessionFactory(
        ICharacterMutationSessionFactory inner) : ICharacterMutationSessionFactory
    {
        public int CallbackCount { get; private set; }

        public int VerificationCount { get; private set; }

        public async ValueTask<ICharacterMutationSession> OpenAsync(
            CancellationToken cancellationToken) =>
            new CountingSession(
                await inner.OpenAsync(cancellationToken),
                this);

        public Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
            CharacterMutationEvidence evidence,
            CancellationToken cancellationToken)
        {
            VerificationCount++;
            Assert.False(cancellationToken.CanBeCanceled);
            return inner.VerifyCommitAsync(evidence, cancellationToken);
        }

        private sealed class CountingSession(
            ICharacterMutationSession innerSession,
            CountingCharacterSessionFactory owner) : ICharacterMutationSession
        {
            public Task<bool> LockActiveUniverseAsync(
                Guid universeId,
                CancellationToken cancellationToken) =>
                innerSession.LockActiveUniverseAsync(universeId, cancellationToken);

            public Task<bool> NameExistsAsync(
                Guid universeId,
                string normalizedName,
                CancellationToken cancellationToken) =>
                innerSession.NameExistsAsync(universeId, normalizedName, cancellationToken);

            public void Add(Character character) => innerSession.Add(character);

            public Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
                Func<CancellationToken, Task<Result<T>>> operation,
                CancellationToken cancellationToken) =>
                innerSession.ExecuteOnceAsync(
                    innerCancellationToken =>
                    {
                        owner.CallbackCount++;
                        return operation(innerCancellationToken);
                    },
                    cancellationToken);

            public ValueTask DisposeAsync() => innerSession.DisposeAsync();
        }
    }

    private sealed class SaveFailureInterceptor : SaveChangesInterceptor
    {
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default) =>
            throw new InjectedSaveException();
    }

    private sealed class CommitAcknowledgementFailureInterceptor(Exception exception)
        : DbTransactionInterceptor
    {
        internal int InvocationCount { get; private set; }

        public override Task TransactionCommittedAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            InvocationCount++;
            return Task.FromException(exception);
        }
    }

    private sealed class RollbackRecordingInterceptor : DbTransactionInterceptor
    {
        internal CancellationToken RollbackToken { get; private set; }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            RollbackToken = cancellationToken;
            return Task.CompletedTask;
        }
    }

    private sealed class RollbackFailureInterceptor : DbTransactionInterceptor
    {
        internal CancellationToken RollbackToken { get; private set; }

        public override Task TransactionRolledBackAsync(
            DbTransaction transaction,
            TransactionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            RollbackToken = cancellationToken;
            throw new InjectedRollbackException();
        }
    }

    private sealed class InjectedSaveException : Exception;

    private sealed class InjectedCommitAcknowledgementException : Exception;

    private sealed class InjectedRollbackException : Exception;

    private sealed class NoOpCleanupRegistry : IMediaCleanupRegistry
    {
        public Task RecordAsync(
            MediaCleanupRegistration registration,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class AdminAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(
                IsAuthenticated: true,
                IsAuthorized: true,
                ActorId: "integration-admin"));
    }

    private sealed class RecordingFileStorage : IFileStorage
    {
        internal string StorageKey { get; } = "catalog/test/compensation.webp";

        internal int CommitCount { get; private set; }

        internal List<string> DeletedKeys { get; } = [];

        internal List<CancellationToken> DeleteTokens { get; } = [];

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken) =>
            Task.FromResult(Result<StagedMediaBatch>.Success(new StagedMediaBatch(
                "test-batch",
                [new StagedMedia(
                    "test-batch",
                    StorageKey,
                    "/media/catalog/test/compensation.webp",
                    "image/webp",
                    3)])));

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
            DeleteTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task<StoredMediaRead?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken) => Task.FromResult<StoredMediaRead?>(null);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
