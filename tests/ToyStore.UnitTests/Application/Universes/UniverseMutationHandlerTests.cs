using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Universes;
using ToyStore.Application.Universes.ArchiveUniverse;
using ToyStore.Application.Universes.CreateUniverse;
using ToyStore.Application.Universes.UpdateUniverse;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Universes;

public sealed class UniverseMutationHandlerTests
{
    [Fact]
    public async Task AnonymousCreateStopsBeforeStorageAndSession()
    {
        var harness = new Harness();
        var command = new CreateUniverseCommand("จักรวาล", "Universe", Upload());
        var behavior = new AuthorizationBehavior<
            CreateUniverseCommand,
            Result<UniverseMutationResult>>(new StubAuthorization(false));

        var result = await behavior.Handle(
            command,
            cancellationToken => harness.Create.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal(0, harness.Session.ExecutionCount);
    }

    [Fact]
    public async Task CreateLocksChecksNamesAllocatesSlugAndPersistsThaiLogoAlt()
    {
        var harness = new Harness();

        var result = await harness.CreateAsync(new CreateUniverseCommand(
            "  จักรวาลใหม่  ",
            "New Universe",
            Upload()));

        Assert.True(result.IsSuccess);
        Assert.Equal("new-universe", result.Value.Slug);
        Assert.Equal(1, result.Value.Version);
        var universe = Assert.IsType<Universe>(harness.Session.AddedUniverse);
        Assert.Equal("จักรวาลใหม่", universe.DisplayName);
        Assert.Equal("admin-1", universe.CreatedBy);
        Assert.Equal("โลโก้จักรวาล จักรวาลใหม่", universe.Logo!.AltText);
        Assert.Equal("new", universe.Logo.StorageKey);
        Assert.Equal(
            ["lock", "display-check", "english-check", "slug", "add"],
            harness.Session.Events);
    }

    [Fact]
    public async Task SeedWithoutLogoRequiresReplacementAndReplacementMakesItReady()
    {
        var seed = Universe.Create(
            CatalogSeedIds.MarvelUniverse,
            "มาร์เวล",
            "Marvel",
            CatalogSlug.Create("marvel"),
            UtcNow,
            "seed");
        var missingHarness = new Harness(seed);

        var missing = await missingHarness.UpdateAsync(new UpdateUniverseCommand(
            seed.Id,
            seed.Version,
            seed.DisplayName,
            seed.EnglishName,
            ReplacementLogo: null));

        Assert.Equal(UniverseErrors.MissingMedia, missing.Error);
        Assert.Equal(0, missingHarness.Storage.StageCount);
        Assert.Null(seed.Logo);

        var replacementHarness = new Harness(seed);
        var replaced = await replacementHarness.UpdateAsync(new UpdateUniverseCommand(
            seed.Id,
            seed.Version,
            "มาร์เวล",
            "Marvel",
            Upload()));

        Assert.True(replaced.IsSuccess);
        Assert.Equal(2, seed.Version);
        Assert.Equal("new", seed.Logo!.StorageKey);
        Assert.Equal("โลโก้จักรวาล มาร์เวล", seed.Logo.AltText);
        Assert.True(seed.CanBeUsedByPublishedProduct);
    }

    [Fact]
    public async Task RetainedLogoRenameUpdatesAltWithoutStagingOrSlugAllocation()
    {
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(),
            "ชื่อเดิม",
            "Same English",
            CatalogSlug.Create("same-english"),
            Media("old", "โลโก้จักรวาล ชื่อเดิม"),
            UtcNow,
            "creator");
        var harness = new Harness(universe);

        var result = await harness.UpdateAsync(new UpdateUniverseCommand(
            universe.Id,
            universe.Version,
            "ชื่อใหม่",
            universe.EnglishName,
            ReplacementLogo: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.Equal("same-english", universe.Slug.Value);
        Assert.Equal("old", universe.Logo!.StorageKey);
        Assert.Equal("โลโก้จักรวาล ชื่อใหม่", universe.Logo.AltText);
        Assert.DoesNotContain("slug", harness.Session.Events);
        Assert.Equal(2, universe.Version);
    }

    [Fact]
    public async Task NoOpWithoutReplacementPreservesLegacyLogoAndVersion()
    {
        var legacyLogo = Media("legacy", "Marvel logo");
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(),
            "มาร์เวล",
            "Marvel",
            CatalogSlug.Create("marvel"),
            legacyLogo,
            UtcNow,
            "creator");
        var harness = new Harness(universe);

        var result = await harness.UpdateAsync(new UpdateUniverseCommand(
            universe.Id,
            universe.Version,
            $" {universe.DisplayName} ",
            $" {universe.EnglishName} ",
            ReplacementLogo: null));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Value.Version);
        Assert.Equal(1, universe.Version);
        var persistedLogo = Assert.IsType<CatalogMediaReference>(universe.Logo);
        Assert.Same(legacyLogo, persistedLogo);
        Assert.Equal("Marvel logo", persistedLogo.AltText);
        Assert.Equal(0, harness.Storage.StageCount);
        Assert.DoesNotContain("slug", harness.Session.Events);
    }

    [Fact]
    public async Task ReplacementReallocatesSlugAndDeletesPreviousLogoOnlyAfterCommit()
    {
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(),
            "ชื่อเดิม",
            "Old English",
            CatalogSlug.Create("old-english"),
            Media("old", "โลโก้เดิม"),
            UtcNow,
            "creator");
        var harness = new Harness(universe);

        var result = await harness.UpdateAsync(new UpdateUniverseCommand(
            universe.Id,
            universe.Version,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.True(result.IsSuccess);
        Assert.Equal("new-english", result.Value.Slug);
        Assert.Equal("new", universe.Logo!.StorageKey);
        Assert.Equal("โลโก้จักรวาล ชื่อใหม่", universe.Logo.AltText);
        Assert.Equal(["old"], harness.Storage.DeletedKeys);
        Assert.Equal(1, harness.Storage.CommitCount);
        Assert.Equal(2, universe.Version);
    }

    [Fact]
    public async Task PostCommitOldLogoDeleteFailureRecordsCleanupAndKeepsSuccess()
    {
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(),
            "ชื่อเดิม",
            "Old English",
            CatalogSlug.Create("old-english"),
            Media("old", "โลโก้เดิม"),
            UtcNow,
            "creator");
        var harness = new Harness(universe);
        harness.Storage.DeleteException = new IOException("disk unavailable");

        var result = await harness.UpdateAsync(new UpdateUniverseCommand(
            universe.Id,
            universe.Version,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.True(result.IsSuccess);
        Assert.Equal(2, universe.Version);
        var cleanup = Assert.Single(harness.Registry.Registrations);
        Assert.Equal("old", cleanup.StorageKey.Value);
        Assert.Equal(MediaCleanupReason.DeleteFailed, cleanup.Reason);
        Assert.Empty(harness.Storage.DeletedKeys);
    }

    [Fact]
    public async Task StaleReplacementDiscardsStagingAndPreservesCurrentLogo()
    {
        var universe = Universe.CreateWithLogo(
            Guid.NewGuid(),
            "ชื่อเดิม",
            "Old English",
            CatalogSlug.Create("old-english"),
            Media("old", "โลโก้เดิม"),
            UtcNow,
            "creator");
        var harness = new Harness(universe);

        var result = await harness.UpdateAsync(new UpdateUniverseCommand(
            universe.Id,
            universe.Version + 1,
            "ชื่อใหม่",
            "New English",
            Upload()));

        Assert.Equal(UniverseErrors.StaleVersion, result.Error);
        Assert.Equal(1, harness.Storage.DiscardCount);
        Assert.Equal(0, harness.Storage.CommitCount);
        Assert.Empty(harness.Storage.DeletedKeys);
        Assert.Equal("old", universe.Logo!.StorageKey);
        Assert.Equal(1, universe.Version);
    }

    [Fact]
    public async Task UpdateReturnsDistinctNotFoundArchivedAndDuplicateFailures()
    {
        var missing = await new Harness().UpdateAsync(new UpdateUniverseCommand(
            Guid.NewGuid(), 1, "ชื่อ", "English", null));
        Assert.Equal(UniverseErrors.NotFound, missing.Error);

        var archived = Universe.Create(
            Guid.NewGuid(), "เก็บแล้ว", "Archived", CatalogSlug.Create("archived"),
            UtcNow, "creator");
        archived.Archive(UtcNow.AddMinutes(1), "archiver");
        var archivedResult = await new Harness(archived).UpdateAsync(new UpdateUniverseCommand(
            archived.Id, archived.Version, "ชื่อใหม่", "New English", null));
        Assert.Equal(UniverseErrors.Archived, archivedResult.Error);

        var active = Universe.CreateWithLogo(
            Guid.NewGuid(), "ชื่อ", "English", CatalogSlug.Create("english"),
            Media("active", "โลโก้"), UtcNow, "creator");
        var duplicateHarness = new Harness(active);
        duplicateHarness.Session.DisplayNameExists = true;
        var duplicate = await duplicateHarness.UpdateAsync(new UpdateUniverseCommand(
            active.Id, active.Version, "ชื่อซ้ำ", active.EnglishName, null));
        Assert.Equal(UniverseErrors.DuplicateDisplayName, duplicate.Error);
    }

    [Fact]
    public async Task ArchiveAppliesSameRuleToSeedAndCustomAndNeverTouchesLogoStorage()
    {
        var seed = Universe.Create(
            CatalogSeedIds.DcUniverse,
            "ดีซี",
            "DC",
            CatalogSlug.Create("dc"),
            UtcNow,
            "seed");
        var seedHarness = new Harness(seed);
        var seedResult = await seedHarness.ArchiveAsync(
            new ArchiveUniverseCommand(seed.Id, seed.Version));
        Assert.True(seedResult.IsSuccess);
        Assert.Equal(CatalogReferenceStatus.Archived, seed.Status);
        Assert.Null(seed.Logo);

        var custom = Universe.CreateWithLogo(
            Guid.NewGuid(), "จักรวาล", "Universe", CatalogSlug.Create("universe"),
            Media("keep", "โลโก้"), UtcNow, "creator");
        var customHarness = new Harness(custom);
        var customResult = await customHarness.ArchiveAsync(
            new ArchiveUniverseCommand(custom.Id, custom.Version));
        Assert.True(customResult.IsSuccess);
        Assert.Equal("keep", custom.Logo!.StorageKey);
        Assert.Equal(0, customHarness.Storage.StageCount);
        Assert.Empty(customHarness.Storage.DeletedKeys);
        Assert.DoesNotContain("lock", customHarness.Session.Events);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff, 1, 2, 3]), "image/jpeg");

    private static CatalogMediaReference Media(string key, string alt) =>
        CatalogMediaReference.Create(key, $"/media/{key}.webp", alt);

    private sealed class Harness
    {
        public Harness(Universe? current = null)
        {
            Session = new FakeSession(current);
            Factory = new FakeFactory(Session);
            Coordinator = new MediaMutationCoordinator(
                Storage,
                new AlwaysUnreferencedVerifier(),
                Registry,
                NullLogger<MediaMutationCoordinator>.Instance);
            var resolver = new CatalogCommitOutcomeResolver(
                NullLogger<CatalogCommitOutcomeResolver>.Instance);
            Create = new CreateUniverseHandler(Factory, Coordinator, new FixedTimeProvider());
            Update = new UpdateUniverseHandler(
                Factory, Coordinator, resolver, new FixedTimeProvider());
            Archive = new ArchiveUniverseHandler(
                Factory, resolver, new FixedTimeProvider());
        }

        public FakeStorage Storage { get; } = new();
        public RecordingCleanupRegistry Registry { get; } = new();
        public FakeSession Session { get; }
        public FakeFactory Factory { get; }
        public MediaMutationCoordinator Coordinator { get; }
        public CreateUniverseHandler Create { get; }
        public UpdateUniverseHandler Update { get; }
        public ArchiveUniverseHandler Archive { get; }

        public Task<Result<UniverseMutationResult>> CreateAsync(CreateUniverseCommand command) =>
            AuthorizeAsync(command, cancellationToken => Create.Handle(command, cancellationToken));

        public Task<Result<UniverseMutationResult>> UpdateAsync(UpdateUniverseCommand command) =>
            AuthorizeAsync(command, cancellationToken => Update.Handle(command, cancellationToken));

        public Task<Result<UniverseMutationResult>> ArchiveAsync(ArchiveUniverseCommand command) =>
            AuthorizeAsync(command, cancellationToken => Archive.Handle(command, cancellationToken));

        private static Task<Result<UniverseMutationResult>> AuthorizeAsync<TRequest>(
            TRequest request,
            RequestHandlerDelegate<Result<UniverseMutationResult>> next)
            where TRequest : AuthorizedUniverseMutationRequest<Result<UniverseMutationResult>>
        {
            var behavior = new AuthorizationBehavior<
                TRequest,
                Result<UniverseMutationResult>>(new StubAuthorization(true));
            return behavior.Handle(request, next, TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeFactory(FakeSession session) : IUniverseMutationSessionFactory
    {
        public ValueTask<IUniverseMutationSession> OpenAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult<IUniverseMutationSession>(session);

        public Task<CatalogCommitVerification<UniverseMutationEvidence>> VerifyCommitAsync(
            UniverseMutationEvidence evidence,
            CancellationToken cancellationToken) =>
            Task.FromResult(CatalogCommitVerificationResult.Committed(evidence));
    }

    private sealed class FakeSession(Universe? current) : IUniverseMutationSession
    {
        public List<string> Events { get; } = [];
        public int ExecutionCount { get; private set; }
        public bool DisplayNameExists { get; set; }
        public bool EnglishNameExists { get; set; }
        public Universe? AddedUniverse { get; private set; }

        public Task AcquireMutationLockAsync(CancellationToken cancellationToken)
        {
            Events.Add("lock");
            return Task.CompletedTask;
        }

        public Task<Universe?> FindAsync(Guid id, CancellationToken cancellationToken)
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

        public void Add(Universe universe)
        {
            Events.Add("add");
            AddedUniverse = universe;
        }

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
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
        public Exception? DeleteException { get; set; }
        public List<string> DeletedKeys { get; } = [];

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

    public sealed class RecordingCleanupRegistry : IMediaCleanupRegistry
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

    private sealed class StubAuthorization(bool allowed) : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(allowed
                ? new CurrentUserAuthorizationResult(true, true, "admin-1")
                : new CurrentUserAuthorizationResult(false, false, null));
    }
}
