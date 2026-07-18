using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.CreateBrand;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Brands;

public sealed class CreateBrandTests
{
    [Fact]
    public async Task ValidatorRequiresValidNamesAndExactlyOneImage()
    {
        var validator = new CreateBrandValidator();
        var command = new CreateBrandCommand(" ", "ภาษาไทย", null);

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateBrandCommand.DisplayName)
            && failure.ErrorMessage == "กรุณากรอกชื่อแบรนด์");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateBrandCommand.EnglishName)
            && failure.ErrorMessage == "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateBrandCommand.Image)
            && failure.ErrorMessage == "กรุณาเลือกรูปภาพแบรนด์");
    }

    [Fact]
    public async Task ValidatorRejectsTrimmedAndNormalizedNamesBeyondLimit()
    {
        var validator = new CreateBrandValidator();
        var command = new CreateBrandCommand(
            new string('ก', CatalogReferenceLimits.NameLength + 1),
            new string('Ａ', CatalogReferenceLimits.NameLength + 1),
            Upload());

        var result = await validator.ValidateAsync(command, TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateBrandCommand.DisplayName)
            && failure.ErrorMessage == "ชื่อแบรนด์ต้องไม่เกิน 200 ตัวอักษร");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(CreateBrandCommand.EnglishName)
            && failure.ErrorMessage == "ชื่อภาษาอังกฤษต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ");
    }

    [Fact]
    public async Task AnonymousRequestStopsBeforeStorageAndHandler()
    {
        var harness = new Harness();
        var command = new CreateBrandCommand("แบรนด์", "Brand", Upload());
        var authorization = new AuthorizationBehavior<CreateBrandCommand, Result<BrandMutationResult>>(
            new StubAuthorization(new CurrentUserAuthorizationResult(false, false, null)));

        var result = await authorization.Handle(
            command,
            cancellationToken => harness.Handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal(0, harness.Session.ExecutionCount);
    }

    [Fact]
    public async Task HappyPathLocksChecksBothNamesAllocatesSlugAndCommitsThaiAlt()
    {
        var harness = new Harness();
        var command = new CreateBrandCommand("  แบรนด์ใหม่  ", "New Brand", Upload());

        var result = await harness.AuthorizeAndHandleAsync(command);

        Assert.True(result.IsSuccess);
        Assert.Equal("new-brand", result.Value.Slug);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(CatalogReferenceStatus.Active, result.Value.Status);
        var brand = Assert.IsType<Brand>(harness.Session.AddedBrand);
        Assert.Equal("แบรนด์ใหม่", brand.DisplayName);
        Assert.Equal("admin-1", brand.CreatedBy);
        Assert.Equal("รูปแบรนด์ แบรนด์ใหม่", brand.Image!.AltText);
        Assert.Equal(harness.Storage.StorageKey, brand.Image.StorageKey);
        Assert.Equal(
            ["lock", "display-check", "english-check", "slug", "add", "media-commit"],
            harness.Events);
    }

    [Theory]
    [InlineData(true, false, "Brand.DuplicateDisplayName")]
    [InlineData(false, true, "Brand.DuplicateEnglishName")]
    public async Task DuplicateNameReturnsTypedFailureAndDiscardsStaging(
        bool displayDuplicate,
        bool englishDuplicate,
        string expectedCode)
    {
        var harness = new Harness
        {
            Session =
            {
                DisplayNameExists = displayDuplicate,
                EnglishNameExists = englishDuplicate,
            },
        };

        var result = await harness.AuthorizeAndHandleAsync(
            new CreateBrandCommand("แบรนด์ซ้ำ", "Duplicate Brand", Upload()));

        Assert.Equal(expectedCode, result.Error.Code);
        Assert.Equal(0, harness.Storage.CommitCount);
        Assert.Equal(1, harness.Storage.DiscardCount);
        Assert.Null(harness.Session.AddedBrand);
    }

    [Fact]
    public async Task StorageValidationFailureMapsToImageFieldWithoutOpeningSession()
    {
        var harness = new Harness();
        harness.Storage.StageFailure = MediaStorageErrors.InvalidSignature;

        var result = await harness.AuthorizeAndHandleAsync(
            new CreateBrandCommand("แบรนด์", "Brand", Upload()));

        Assert.Equal(MediaStorageErrors.InvalidSignature, result.Error);
        var failure = Assert.Single(result.ValidationFailures);
        Assert.Equal(nameof(CreateBrandCommand.Image), failure.PropertyName);
        Assert.Equal(MediaStorageErrors.InvalidSignature.Message, failure.ErrorMessage);
        Assert.Equal(0, harness.Session.ExecutionCount);
    }

    [Fact]
    public void CommandMapsOnlyBrandPersistenceFailuresAndBypassesAutomaticTransaction()
    {
        var command = new CreateBrandCommand("แบรนด์", "Brand", Upload());
        var persistenceRequest = Assert.IsAssignableFrom<
            IPersistenceFailureResultRequest<Result<BrandMutationResult>>>(command);

        Assert.Equal(
            BrandErrors.DuplicateDisplayName,
            persistenceRequest.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Brand,
                PersistenceFailureKind.DuplicateDisplayName)));
        Assert.Equal(
            BrandErrors.StaleVersion,
            persistenceRequest.MapPersistenceFailure(new PersistenceFailure(
                PersistenceFailureTarget.Request,
                PersistenceFailureKind.ConcurrencyConflict)));
        Assert.Null(persistenceRequest.MapPersistenceFailure(new PersistenceFailure(
            PersistenceFailureTarget.Universe,
            PersistenceFailureKind.DuplicateDisplayName)));
        Assert.DoesNotContain(
            command.GetType().GetInterfaces(),
            contract => contract.IsGenericType
                && contract.GetGenericTypeDefinition().Name == "ICommand`1");
        Assert.Equal(PolicyNames.CanManageProducts, command.RequiredPolicy);
    }

    [Fact]
    public void AddApplicationRegistersSharedMutationCoordinatorsForMediatRHandlers()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(MediaMutationCoordinator)
                && descriptor.Lifetime == ServiceLifetime.Transient);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(CatalogCommitOutcomeResolver)
                && descriptor.Lifetime == ServiceLifetime.Transient);
    }

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private sealed class Harness
    {
        public Harness()
        {
            SessionFactory = new FakeBrandMutationSessionFactory(Session);
            Coordinator = new MediaMutationCoordinator(
                Storage,
                new AlwaysUnreferencedVerifier(),
                new NoOpCleanupRegistry(),
                NullLogger<MediaMutationCoordinator>.Instance);
            Handler = new CreateBrandHandler(
                SessionFactory,
                Coordinator,
                new FixedTimeProvider());
        }

        public List<string> Events { get; } = [];

        public FakeStorage Storage { get; } = new();

        public FakeBrandMutationSession Session { get; } = new();

        public FakeBrandMutationSessionFactory SessionFactory { get; }

        public MediaMutationCoordinator Coordinator { get; }

        public CreateBrandHandler Handler { get; }

        public async Task<Result<BrandMutationResult>> AuthorizeAndHandleAsync(
            CreateBrandCommand command)
        {
            Storage.Events = Events;
            Session.Events = Events;
            var behavior = new AuthorizationBehavior<CreateBrandCommand, Result<BrandMutationResult>>(
                new StubAuthorization(new CurrentUserAuthorizationResult(true, true, "admin-1")));
            return await behavior.Handle(
                command,
                cancellationToken => Handler.Handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeBrandMutationSessionFactory(FakeBrandMutationSession session)
        : IBrandMutationSessionFactory
    {
        public ValueTask<IBrandMutationSession> OpenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IBrandMutationSession>(session);

        public Task<CatalogCommitVerification<BrandMutationEvidence>> VerifyCommitAsync(
            BrandMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(CatalogCommitVerificationResult.Committed(evidence));
    }

    private sealed class FakeBrandMutationSession : IBrandMutationSession
    {
        private bool active;

        public List<string> Events { get; set; } = [];

        public int ExecutionCount { get; private set; }

        public bool DisplayNameExists { get; set; }

        public bool EnglishNameExists { get; set; }

        public Brand? AddedBrand { get; private set; }

        public Task AcquireMutationLockAsync(CancellationToken cancellationToken)
        {
            Assert.True(active);
            Events.Add("lock");
            return Task.CompletedTask;
        }

        public Task<Brand?> FindAsync(Guid id, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DisplayNameExistsAsync(
            string normalizedDisplayName,
            Guid? excludedId,
            CancellationToken cancellationToken)
        {
            Events.Add("display-check");
            return Task.FromResult(DisplayNameExists);
        }

        public Task<bool> EnglishNameExistsAsync(
            string normalizedEnglishName,
            Guid? excludedId,
            CancellationToken cancellationToken)
        {
            Events.Add("english-check");
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

        public void Add(Brand brand)
        {
            Events.Add("add");
            AddedBrand = brand;
        }

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            active = true;
            try
            {
                var result = await operation(cancellationToken);
                return new CatalogMutationExecution<T>(
                    result,
                    result.IsSuccess
                        ? CatalogCommitOutcome.Committed
                        : CatalogCommitOutcome.DefinitelyRolledBack);
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
        public string StorageKey { get; } = "brands/new.webp";

        public List<string> Events { get; set; } = [];

        public Error? StageFailure { get; set; }

        public int StageCount { get; private set; }

        public int CommitCount { get; private set; }

        public int DiscardCount { get; private set; }

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            StageCount++;
            if (StageFailure is not null)
            {
                return Task.FromResult(Result<StagedMediaBatch>.Failure(StageFailure));
            }

            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(
                    "batch",
                    [new StagedMedia("batch", StorageKey, "/media/brands/new.webp", "image/webp", 10)])));
        }

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
        {
            CommitCount++;
            Events.Add("media-commit");
            return Task.CompletedTask;
        }

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken)
        {
            DiscardCount++;
            return Task.CompletedTask;
        }

        public Task DeleteCommittedAsync(
            IReadOnlyCollection<string> storageKeys,
            CancellationToken cancellationToken) => Task.CompletedTask;

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
        public override DateTimeOffset GetUtcNow() =>
            new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
    }

    private sealed class StubAuthorization(CurrentUserAuthorizationResult result)
        : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }
}
