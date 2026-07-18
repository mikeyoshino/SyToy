using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Brands;
using ToyStore.Application.Brands.UpdateBrand;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Brands;

public sealed class UpdateBrandTests
{
    [Fact]
    public async Task ValidatorRequiresIdentityVersionValidNamesAndOptionalImage()
    {
        var validator = new UpdateBrandValidator();

        var result = await validator.ValidateAsync(
            new UpdateBrandCommand(Guid.Empty, 0, " ", "ภาษาไทย", null),
            TestContext.Current.CancellationToken);

        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateBrandCommand.Id)
            && failure.ErrorMessage == "รหัสแบรนด์ไม่ถูกต้อง");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateBrandCommand.ExpectedVersion)
            && failure.ErrorMessage == "เวอร์ชันข้อมูลแบรนด์ไม่ถูกต้อง");
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateBrandCommand.DisplayName));
        Assert.Contains(result.Errors, failure =>
            failure.PropertyName == nameof(UpdateBrandCommand.EnglishName)
            && failure.ErrorMessage == "ชื่อภาษาอังกฤษต้องสร้างส่วน URL ได้ด้วยตัวอักษรอังกฤษหรือตัวเลข");
    }

    [Fact]
    public async Task RetainImageUpdatesThaiAltWithoutStagingAndKeepsSlugWhenEnglishUnchanged()
    {
        var current = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อเดิม", "Same English", CatalogSlug.Create("same-english"),
            Media("old", "รูปแบรนด์ ชื่อเดิม"), UtcNow, "creator");
        var harness = new Harness(current);

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            current.Version,
            "ชื่อใหม่",
            current.EnglishName,
            ReplacementImage: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal("same-english", current.Slug.Value);
        Assert.Equal("old", current.Image!.StorageKey);
        Assert.Equal("รูปแบรนด์ ชื่อใหม่", current.Image.AltText);
        Assert.Equal(2, current.Version);
        Assert.DoesNotContain("slug", harness.Session.Events);
    }

    [Fact]
    public async Task NoOpWithoutReplacementPreservesLegacyImageAndVersion()
    {
        var legacyImage = Media("legacy", "Legacy Brand artwork");
        var current = Brand.CreateWithImage(
            Guid.NewGuid(),
            "ชื่อเดิม",
            "Same English",
            CatalogSlug.Create("same-english"),
            legacyImage,
            UtcNow,
            "creator");
        var harness = new Harness(current);

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            current.Version,
            $" {current.DisplayName} ",
            $" {current.EnglishName} ",
            ReplacementImage: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(1, current.Version);
        Assert.Same(legacyImage, current.Image);
        Assert.Equal("Legacy Brand artwork", current.Image?.AltText);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.DoesNotContain("slug", harness.Session.Events);
    }

    [Fact]
    public async Task ReplacementReallocatesChangedEnglishSlugAndDeletesOldAfterCommit()
    {
        var current = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อเดิม", "Old English", CatalogSlug.Create("old-english"),
            Media("old", "รูปเดิม"), UtcNow, "creator");
        var harness = new Harness(current);

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            current.Version,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.True(result.IsSuccess);
        Assert.Equal("new-english", result.Value.Slug);
        Assert.Equal("new", current.Image!.StorageKey);
        Assert.Equal("รูปแบรนด์ ชื่อใหม่", current.Image.AltText);
        Assert.Equal(["old"], harness.Storage.DeletedKeys);
        Assert.Equal(1, harness.Storage.CommitCount);
        Assert.Equal(2, current.Version);
    }

    [Fact]
    public async Task PostCommitOldImageDeleteFailureRecordsCleanupAndKeepsSuccess()
    {
        var current = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อเดิม", "Old English", CatalogSlug.Create("old-english"),
            Media("old", "รูปเดิม"), UtcNow, "creator");
        var harness = new Harness(current);
        harness.Storage.DeleteException = new IOException("disk unavailable");

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            current.Version,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, current.Version);
        var cleanup = Assert.Single(harness.Registry.Registrations);
        Assert.Equal("old", cleanup.StorageKey.Value);
        Assert.Equal(MediaCleanupReason.DeleteFailed, cleanup.Reason);
        Assert.Empty(harness.Storage.DeletedKeys);
    }

    [Fact]
    public async Task ReplacementStaleVersionReturnsTypedFailureAndOnlyDiscardsStaging()
    {
        var current = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อเดิม", "Old English", CatalogSlug.Create("old-english"),
            Media("old", "รูปเดิม"), UtcNow, "creator");
        var harness = new Harness(current);

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            ExpectedVersion: current.Version + 1,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.Equal(BrandErrors.StaleVersion, result.Error);
        Assert.Equal(1, harness.Storage.DiscardCount);
        Assert.Equal(0, harness.Storage.CommitCount);
        Assert.Empty(harness.Storage.DeletedKeys);
        Assert.Equal("old", current.Image!.StorageKey);
        Assert.Equal(1, current.Version);
    }

    [Fact]
    public async Task MissingCurrentImageWithoutReplacementReturnsTypedFailure()
    {
        var current = Brand.Create(
            Guid.NewGuid(), "ชื่อเดิม", "Old English", CatalogSlug.Create("old-english"),
            UtcNow, "creator");
        var harness = new Harness(current);

        var result = await harness.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            current.Id,
            current.Version,
            current.DisplayName,
            current.EnglishName,
            ReplacementImage: null));

        Assert.Equal(BrandErrors.MissingMedia, result.Error);
        Assert.Equal(0, harness.Storage.StageCount);
    }

    [Fact]
    public async Task NotFoundArchivedAndDuplicatePathsReturnTypedResults()
    {
        var notFound = new Harness(current: null);
        var missing = await notFound.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            Guid.NewGuid(), 1, "ชื่อ", "English", null));
        Assert.Equal(BrandErrors.NotFound, missing.Error);

        var archivedBrand = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อ", "English", CatalogSlug.Create("english"),
            Media("archived", "รูป"), UtcNow, "creator");
        archivedBrand.Archive(UtcNow.AddMinutes(1), "archiver");
        var archived = new Harness(archivedBrand);
        var archivedResult = await archived.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            archivedBrand.Id, archivedBrand.Version, "ชื่อใหม่", "New English", null));
        Assert.Equal(BrandErrors.Archived, archivedResult.Error);

        var duplicateBrand = Brand.CreateWithImage(
            Guid.NewGuid(), "ชื่อ", "English", CatalogSlug.Create("english"),
            Media("duplicate", "รูป"), UtcNow, "creator");
        var duplicate = new Harness(duplicateBrand);
        duplicate.Session.DisplayNameExists = true;
        var duplicateResult = await duplicate.AuthorizeAndHandleAsync(new UpdateBrandCommand(
            duplicateBrand.Id, duplicateBrand.Version, "ชื่อซ้ำ", "English", null));
        Assert.Equal(BrandErrors.DuplicateDisplayName, duplicateResult.Error);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private static CatalogMediaReference Media(string key, string alt) =>
        CatalogMediaReference.Create(key, $"/media/{key}.webp", alt);

    private sealed class Harness
    {
        public Harness(Brand? current)
        {
            Session = new FakeSession(current);
            Factory = new FakeFactory(Session);
            Coordinator = new MediaMutationCoordinator(
                Storage,
                new AlwaysUnreferencedVerifier(),
                Registry,
                NullLogger<MediaMutationCoordinator>.Instance);
            Handler = new UpdateBrandHandler(
                Factory,
                Coordinator,
                new CatalogCommitOutcomeResolver(
                    NullLogger<CatalogCommitOutcomeResolver>.Instance),
                new FixedTimeProvider());
        }

        public FakeStorage Storage { get; } = new();

        public CapturingCleanupRegistry Registry { get; } = new();

        public FakeSession Session { get; }

        public FakeFactory Factory { get; }

        public MediaMutationCoordinator Coordinator { get; }

        public UpdateBrandHandler Handler { get; }

        public async Task<Result<BrandMutationResult>> AuthorizeAndHandleAsync(
            UpdateBrandCommand command)
        {
            var authorization = new AuthorizationBehavior<UpdateBrandCommand, Result<BrandMutationResult>>(
                new StubAuthorization());
            return await authorization.Handle(
                command,
                cancellationToken => Handler.Handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeFactory(FakeSession session) : IBrandMutationSessionFactory
    {
        public ValueTask<IBrandMutationSession> OpenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IBrandMutationSession>(session);

        public Task<CatalogCommitVerification<BrandMutationEvidence>> VerifyCommitAsync(
            BrandMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(CatalogCommitVerificationResult.Committed(evidence));
    }

    private sealed class FakeSession(Brand? current) : IBrandMutationSession
    {
        public List<string> Events { get; } = [];

        public bool DisplayNameExists { get; set; }

        public bool EnglishNameExists { get; set; }

        public Task AcquireMutationLockAsync(CancellationToken cancellationToken)
        {
            Events.Add("lock");
            return Task.CompletedTask;
        }

        public Task<Brand?> FindAsync(Guid id, CancellationToken cancellationToken)
        {
            Events.Add("find");
            return Task.FromResult(current?.Id == id ? current : null);
        }

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

        public void Add(Brand brand) => throw new NotSupportedException();

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

    private sealed class FakeStorage : IFileStorage
    {
        public int StageCount { get; private set; }

        public int CommitCount { get; private set; }

        public int DiscardCount { get; private set; }

        public List<string> DeletedKeys { get; } = [];

        public Exception? DeleteException { get; set; }

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            StageCount++;
            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(
                    "batch",
                    [new StagedMedia("batch", "new", "/media/new.webp", "image/webp", 10)])));
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
            if (DeleteException is not null)
            {
                throw DeleteException;
            }

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

    private sealed class CapturingCleanupRegistry : IMediaCleanupRegistry
    {
        public List<MediaCleanupRegistration> Registrations { get; } = [];

        public Task RecordAsync(
            MediaCleanupRegistration registration,
            CancellationToken cancellationToken)
        {
            Registrations.Add(registration);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedTimeProvider : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => UtcNow.AddMinutes(2);
    }

    private sealed class StubAuthorization : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CurrentUserAuthorizationResult(true, true, "admin-1"));
    }
}
