using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Domain.Products;

namespace ToyStore.UnitTests.Application.Products;

public sealed class ProductMediaMutationCoordinatorTests
{
    private static readonly ProductMediaMutationContext Context = new(
        Guid.Parse("d5960783-3fba-4ad1-91d9-305d5a3a196f"));

    [Fact]
    public async Task ZeroUploadsBypassesStorageAndStillExecutesOneDatabaseSession()
    {
        var storage = new FakeStorage();
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var coordinator = Create(storage);
        IReadOnlyList<StagedMedia>? observed = null;

        var result = await coordinator.ExecuteAsync<string, string>(
            [], session,
            (media, _) =>
            {
                observed = media;
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(Assert.IsAssignableFrom<IReadOnlyList<StagedMedia>>(observed));
        Assert.Equal(0, storage.StageCount);
        Assert.Equal(0, storage.CommitCount);
        Assert.Equal(1, session.ExecutionCount);
    }

    [Fact]
    public async Task MultipleUploadsStageOncePreserveOrderAndCommitImmediatelyBeforeDatabaseSave()
    {
        var events = new ConcurrentQueue<string>();
        var storage = new FakeStorage(events);
        var session = new FakeSession(CatalogCommitOutcome.Committed, events);
        var coordinator = Create(storage);
        string[]? ordered = null;

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload(), Upload()], session,
            (media, _) =>
            {
                ordered = media.Select(item => item.StorageKey).ToArray();
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [MediaKey(1), MediaKey(2), MediaKey(3)],
            Assert.IsType<string[]>(ordered));
        Assert.Equal(1, storage.StageCount);
        Assert.Equal(1, storage.CommitCount);
        Assert.True(Array.IndexOf(events.ToArray(), "media-commit")
            < Array.IndexOf(events.ToArray(), "database-save"));
    }

    [Fact]
    public async Task PartialStageDescriptorIsDiscardedAndNeverOpensMutationCallback()
    {
        var storage = new FakeStorage { ReturnedMediaCount = 1 };
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var coordinator = Create(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
            _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken));

        Assert.Equal(0, session.ExecutionCount);
        Assert.Single(storage.DiscardTokens);
        Assert.False(storage.DiscardTokens[0].CanBeCanceled);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task MalformedBatchTokenOrDuplicateKeyIsDiscardedBeforeSession(
        bool wrongBatchToken,
        bool duplicateKey)
    {
        var storage = new FakeStorage
        {
            WrongBatchToken = wrongBatchToken,
            DuplicateKey = duplicateKey,
        };
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var coordinator = Create(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
            _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken));

        Assert.Equal(0, session.ExecutionCount);
        Assert.Single(storage.DiscardTokens);
    }

    [Fact]
    public async Task StageFailureReturnsTypedFailureWithoutOpeningSession()
    {
        var expected = new Error("Media.Invalid", "ไฟล์ไม่ถูกต้อง", ErrorType.Validation);
        var storage = new FakeStorage { StageError = expected };
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var coordinator = Create(storage);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
            _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Error);
        Assert.Equal(0, session.ExecutionCount);
        Assert.Equal(0, storage.CommitCount);
    }

    [Fact]
    public async Task TypedMutationFailureDiscardsWholeBatchWithoutCommit()
    {
        var expected = new Error("Product.Duplicate", "สินค้าซ้ำ", ErrorType.Conflict);
        var storage = new FakeStorage();
        var session = new FakeSession(CatalogCommitOutcome.DefinitelyRolledBack);
        var coordinator = Create(storage);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Failure(expected)),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Error);
        Assert.Equal(0, storage.CommitCount);
        Assert.Single(storage.DiscardTokens);
        Assert.False(storage.DiscardTokens[0].CanBeCanceled);
    }

    [Fact]
    public async Task SuccessfulCallbackWithDefinitelyRolledBackOutcomeCompensatesThenFailsClosed()
    {
        var storage = new FakeStorage();
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var coordinator = Create(storage, verifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], new FakeSession(CatalogCommitOutcome.DefinitelyRolledBack),
            (_, _) => Task.FromResult(Result<string>.Success("saved")),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken));

        Assert.Equal([MediaKey(1), MediaKey(2)], storage.DeletedKeys);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CommitOrSessionFailureCompensatesEveryNewKeyWithoutCancellation(
        bool failAfterCommit)
    {
        var failure = new IOException("persistence failed");
        var storage = new FakeStorage
        {
            CommitException = failAfterCommit ? null : failure,
        };
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var session = new FakeSession(CatalogCommitOutcome.Committed)
        {
            ExceptionAfterOperation = failAfterCommit ? failure : null,
        };
        var coordinator = Create(storage, verifier);

        await Assert.ThrowsAsync<IOException>(() => coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("saved")),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            static value => value,
            Context,
            static () => [],
            TestContext.Current.CancellationToken));

        Assert.Equal([MediaKey(1), MediaKey(2)], storage.DeletedKeys);
        Assert.All(storage.DeleteTokens, token => Assert.False(token.CanBeCanceled));
        Assert.Equal(failAfterCommit ? 0 : 1, storage.DiscardTokens.Count);
    }

    [Fact]
    public async Task DurableCommitKeepsNewKeysAndProcessesLockedOldKeysAfterCommitOnly()
    {
        var events = new ConcurrentQueue<string>();
        var storage = new FakeStorage(events);
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var session = new FakeSession(CatalogCommitOutcome.Committed, events);
        var coordinator = Create(storage, verifier);
        IReadOnlyCollection<string> removed = [];

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) =>
            {
                removed = ["products/old-a.webp", "products/old-b.webp"];
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            () => removed,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["products/old-a.webp", "products/old-b.webp"], storage.DeletedKeys);
        Assert.DoesNotContain(MediaKey(1), storage.DeletedKeys);
        Assert.True(Array.IndexOf(events.ToArray(), "database-save")
            < Array.IndexOf(events.ToArray(), "media-delete"));
    }

    [Fact]
    public async Task IndeterminateCommittedKeepsNewKeysProcessesOldKeysAndNeverReplaysCallback()
    {
        var storage = new FakeStorage();
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var session = new FakeSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = Create(storage, verifier);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("original")),
            token =>
            {
                Assert.False(token.CanBeCanceled);
                return Verification(CatalogCommitVerification.Committed, "ignored");
            },
            static value => value,
            Context,
            static () => ["products/old.webp"],
            TestContext.Current.CancellationToken);

        Assert.Equal("original", result.Value);
        Assert.Equal(["products/old.webp"], storage.DeletedKeys);
        Assert.DoesNotContain(MediaKey(1), storage.DeletedKeys);
        Assert.Equal(1, session.CallbackCount);
    }

    [Fact]
    public async Task IndeterminateSupersededRefreshesAndCleansOnlyUnreferencedSubset()
    {
        var storage = new FakeStorage();
        var verifier = new FakeVerifier(new Dictionary<string, MediaReferenceVerification>
        {
            [MediaKey(1)] = MediaReferenceVerification.Referenced,
            [MediaKey(2)] = MediaReferenceVerification.Unreferenced,
            ["products/old-a.webp"] = MediaReferenceVerification.Referenced,
            ["products/old-b.webp"] = MediaReferenceVerification.Unreferenced,
        });
        var session = new FakeSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = Create(storage, verifier);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("original")),
            token =>
            {
                Assert.False(token.CanBeCanceled);
                return Verification(CatalogCommitVerification.Superseded, "authoritative");
            },
            static value => value,
            Context,
            static () => ["products/old-a.webp", "products/old-b.webp"],
            TestContext.Current.CancellationToken);

        Assert.Equal("authoritative", result.Value);
        Assert.Equal([MediaKey(2), "products/old-b.webp"], storage.DeletedKeys);
        Assert.Equal(1, session.CallbackCount);
    }

    [Fact]
    public async Task IndeterminateNotCommittedCompensatesEveryNewKeyButNeverOldKeys()
    {
        var storage = new FakeStorage();
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var session = new FakeSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = Create(storage, verifier);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("original")),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            static value => value,
            Context,
            static () => ["products/old.webp"],
            TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, result.Error);
        Assert.Equal([MediaKey(1), MediaKey(2)], storage.DeletedKeys);
        Assert.DoesNotContain("products/old.webp", storage.DeletedKeys);
    }

    [Theory]
    [InlineData(CatalogCommitVerification.Unavailable)]
    [InlineData(CatalogCommitVerification.Inconsistent)]
    public async Task UnknownCommitProofRecordsEveryNewKeyWithoutDeleting(
        CatalogCommitVerification verification)
    {
        var storage = new FakeStorage();
        var registry = new FakeRegistry();
        var session = new FakeSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = Create(storage, registry: registry);

        var result = await coordinator.ExecuteAsync<string, string>(
            [Upload(), Upload()], session,
            (_, _) => Task.FromResult(Result<string>.Success("original")),
            _ => Verification(verification),
            static value => value,
            Context,
            static () => ["products/old.webp"],
            TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, result.Error);
        Assert.Empty(storage.DeletedKeys);
        Assert.Equal(
            [MediaKey(1), MediaKey(2)],
            registry.Registrations.Select(item => item.StorageKey.Value));
        Assert.All(registry.Registrations, item =>
            Assert.Equal(MediaCleanupReason.CommitOutcomeUnknown, item.Reason));
    }

    [Fact]
    public async Task OldKeyDeleteFailureRegistersPerKeyButPreservesDurableSuccess()
    {
        var storage = new FakeStorage { DeleteException = new IOException("delete failed") };
        var verifier = FakeVerifier.All(MediaReferenceVerification.Unreferenced);
        var registry = new FakeRegistry();
        var coordinator = Create(storage, verifier, registry);

        var result = await coordinator.ExecuteAsync<string, string>(
            [], new FakeSession(CatalogCommitOutcome.Committed),
            (_, _) => Task.FromResult(Result<string>.Success("saved")),
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            static () => ["products/old-a.webp", "products/old-b.webp"],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, registry.Registrations.Count);
        Assert.All(registry.Registrations, item =>
            Assert.Equal(MediaCleanupReason.DeleteFailed, item.Reason));
    }

    [Fact]
    public async Task CleanupRegistryFailureNeverReversesDurableSuccessOrStopsOtherKeys()
    {
        var storage = new FakeStorage { DeleteException = new IOException("delete failed") };
        var registry = new FakeRegistry { Exception = new IOException("ledger failed") };
        var coordinator = Create(
            storage,
            FakeVerifier.All(MediaReferenceVerification.Unreferenced),
            registry);

        var result = await coordinator.ExecuteAsync<string, string>(
            [], new FakeSession(CatalogCommitOutcome.Committed),
            (_, _) => Task.FromResult(Result<string>.Success("saved")),
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            static () => ["products/old-a.webp", "products/old-b.webp"],
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(["products/old-a.webp", "products/old-b.webp"], storage.DeletedKeys);
        Assert.Equal(2, registry.AttemptCount);
    }

    [Fact]
    public async Task StagingCancellationPropagatesWithoutOpeningSessionOrCleanup()
    {
        var cancellation = new OperationCanceledException("stage cancelled");
        var storage = new FakeStorage { StageException = cancellation };
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var coordinator = Create(storage);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.ExecuteAsync<string, string>(
                [Upload()], session,
                (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
                _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
                static value => value,
                Context,
                static () => [],
                TestContext.Current.CancellationToken));

        Assert.Same(cancellation, thrown);
        Assert.Equal(0, session.ExecutionCount);
        Assert.Empty(storage.DiscardTokens);
    }

    [Fact]
    public async Task OrderedPlanInterleavesTrustedRetainedImagesAndUploads()
    {
        var storage = new FakeStorage();
        var fixture = Create(storage);
        var retainedA = new ProductImageDefinition(
            Guid.NewGuid(), "legacy/retained-a.webp", "/media/legacy/retained-a.webp", "A");
        var retainedB = new ProductImageDefinition(
            Guid.NewGuid(), "legacy/retained-b.webp", "/media/legacy/retained-b.webp", "B");
        var before = ProductWithImages([retainedA, retainedB]);
        var beforeSnapshot = ProductMediaSnapshot.Capture(before.Images);
        ProductMediaMutationState? state = null;
        ProductMediaPlanSlot[] plan =
        [
            new RetainedProductMediaSlot(retainedA.Id),
            new UploadProductMediaSlot(Upload()),
            new RetainedProductMediaSlot(retainedB.Id),
            new UploadProductMediaSlot(Upload()),
        ];

        var result = await fixture.Inner.ExecuteAsync<string, string>(
            plan,
            new FakeSession(CatalogCommitOutcome.Committed),
            (resolved, _) =>
            {
                Assert.IsType<ResolvedRetainedProductMediaSlot>(resolved[0]);
                var newA = Assert.IsType<ResolvedUploadProductMediaSlot>(resolved[1]).Media;
                Assert.IsType<ResolvedRetainedProductMediaSlot>(resolved[2]);
                var newB = Assert.IsType<ResolvedUploadProductMediaSlot>(resolved[3]).Media;
                var after = ProductWithImages(
                [
                    retainedA,
                    new ProductImageDefinition(
                        Guid.NewGuid(), newA.StorageKey, newA.PublicRelativeUrl, "ใหม่ A"),
                    retainedB,
                    new ProductImageDefinition(
                        Guid.NewGuid(), newB.StorageKey, newB.PublicRelativeUrl, "ใหม่ B"),
                ]);
                state = ProductMediaMutationState.Capture(beforeSnapshot, after.Images);
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            () => state!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Empty(storage.DeletedKeys);
    }

    [Fact]
    public async Task DurableCommitDeletesStagedUploadNotReferencedByLockedFinalState()
    {
        var storage = new FakeStorage();
        var fixture = Create(storage, FakeVerifier.All(MediaReferenceVerification.Unreferenced));
        ProductMediaMutationState? state = null;
        ProductMediaPlanSlot[] plan =
        [new UploadProductMediaSlot(Upload()), new UploadProductMediaSlot(Upload())];

        var result = await fixture.Inner.ExecuteAsync<string, string>(
            plan,
            new FakeSession(CatalogCommitOutcome.Committed),
            (resolved, _) =>
            {
                var used = Assert.IsType<ResolvedUploadProductMediaSlot>(resolved[0]).Media;
                var after = ProductWithImages(
                [
                    new ProductImageDefinition(
                        Guid.NewGuid(), used.StorageKey, used.PublicRelativeUrl, "ใช้จริง"),
                ]);
                state = ProductMediaMutationState.Capture(
                    ProductMediaSnapshot.Capture(ProductWithImages([]).Images),
                    after.Images);
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            () => state!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([MediaKey(2)], storage.DeletedKeys);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task NullOrInvalidStagedDescriptorIsDiscardedBeforeSession(
        bool nullMedia,
        bool invalidStorageKey)
    {
        var storage = new FakeStorage
        {
            NullMedia = nullMedia,
            InvalidStorageKey = invalidStorageKey,
        };
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var fixture = Create(storage);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Inner.ExecuteAsync<string, string>(
                [new UploadProductMediaSlot(Upload())],
                session,
                (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
                _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
                static value => value,
                Context,
                static () => throw new InvalidOperationException("must not run"),
                TestContext.Current.CancellationToken));

        Assert.Equal(0, session.ExecutionCount);
        Assert.Single(storage.DiscardTokens);
    }

    [Fact]
    public async Task MutablePlanAndUploadsAreSnapshottedBeforeFirstAwait()
    {
        var storage = new FakeStorage();
        var fixture = Create(storage);
        var plan = new List<ProductMediaPlanSlot>
        {
            new UploadProductMediaSlot(Upload()),
            new UploadProductMediaSlot(Upload()),
        };
        storage.BeforeStage = () => plan.Clear();
        ProductMediaMutationState? state = null;

        var result = await fixture.Inner.ExecuteAsync<string, string>(
            plan,
            new FakeSession(CatalogCommitOutcome.Committed),
            (resolved, _) =>
            {
                var staged = resolved.OfType<ResolvedUploadProductMediaSlot>()
                    .Select(slot => slot.Media)
                    .ToArray();
                Assert.Equal(2, staged.Length);
                state = State([], staged);
                return Task.FromResult(Result<string>.Success("saved"));
            },
            _ => Verification(CatalogCommitVerification.Committed, "saved"),
            static value => value,
            Context,
            () => state!,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, storage.LastUploadCount);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InvalidCombinedPlanFailsBeforeStagingOrSession(bool duplicateRetained)
    {
        var storage = new FakeStorage();
        var session = new FakeSession(CatalogCommitOutcome.Committed);
        var fixture = Create(storage);
        var retainedId = Guid.NewGuid();
        ProductMediaPlanSlot[] plan = duplicateRetained
            ? [new RetainedProductMediaSlot(retainedId), new RetainedProductMediaSlot(retainedId)]
            : Enumerable.Range(0, Product.MaximumImageCount + 1)
                .Select(_ => (ProductMediaPlanSlot)new UploadProductMediaSlot(Upload()))
                .ToArray();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            fixture.Inner.ExecuteAsync<string, string>(
                plan,
                session,
                (_, _) => Task.FromResult(Result<string>.Success("unexpected")),
                _ => Verification(CatalogCommitVerification.Committed, "unexpected"),
                static value => value,
                Context,
                static () => throw new InvalidOperationException("must not run"),
                TestContext.Current.CancellationToken));

        Assert.Equal(0, storage.StageCount);
        Assert.Equal(0, session.ExecutionCount);
    }

    [Fact]
    public async Task InvalidLockedPrimaryOrderRollsBackAndDiscardsBeforeMediaCommit()
    {
        var storage = new FakeStorage();
        var fixture = Create(storage);
        ProductMediaMutationState? state = null;

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Inner.ExecuteAsync<string, string>(
                [new UploadProductMediaSlot(Upload())],
                new FakeSession(CatalogCommitOutcome.Committed),
                (resolved, _) =>
                {
                    var staged = Assert.IsType<ResolvedUploadProductMediaSlot>(resolved[0]).Media;
                    var after = ProductWithImages(
                    [
                        new ProductImageDefinition(
                            Guid.NewGuid(), staged.StorageKey, staged.PublicRelativeUrl, "ภาพ"),
                    ]);
                    typeof(ProductImage).GetProperty(nameof(ProductImage.SortOrder))!
                        .SetValue(after.Images[0], 1);
                    state = ProductMediaMutationState.Capture(
                        ProductMediaSnapshot.Capture(ProductWithImages([]).Images),
                        after.Images);
                    return Task.FromResult(Result<string>.Success("saved"));
                },
                _ => Verification(CatalogCommitVerification.Committed, "saved"),
                static value => value,
                Context,
                () => state!,
                TestContext.Current.CancellationToken));

        Assert.Equal(0, storage.CommitCount);
        Assert.Single(storage.DiscardTokens);
    }

    [Fact]
    public async Task CommitCancellationVerifiesAndCleansNonCancellablyWithoutReplayingCallback()
    {
        var cancellation = new OperationCanceledException("commit acknowledgement cancelled");
        var storage = new FakeStorage();
        var session = new FakeSession(CatalogCommitOutcome.Indeterminate)
        {
            CommitFailureException = cancellation,
        };
        var coordinator = Create(storage);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.ExecuteAsync<string, string>(
                [Upload()], session,
                (_, _) => Task.FromResult(Result<string>.Success("saved")),
                token =>
                {
                    Assert.False(token.CanBeCanceled);
                    return Verification(CatalogCommitVerification.Committed, "saved");
                },
                static value => value,
                Context,
                static () => [],
                new CancellationToken(canceled: true)));

        Assert.Same(cancellation, thrown);
        Assert.Equal(1, session.CallbackCount);
    }

    private static CoordinatorFixture Create(
        FakeStorage storage,
        FakeVerifier? verifier = null,
        FakeRegistry? registry = null) =>
        new(new ProductMediaMutationCoordinator(
                storage,
                verifier ?? FakeVerifier.All(MediaReferenceVerification.Referenced),
                registry ?? new FakeRegistry(),
                NullLogger<ProductMediaMutationCoordinator>.Instance));

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private static readonly string BatchToken = new('a', 32);

    private static string MediaKey(int index) =>
        $"{BatchToken}/{index:x32}.webp";

    private static ProductMediaMutationState State(
        IReadOnlyCollection<string> beforeKeys,
        IReadOnlyCollection<StagedMedia> afterMedia)
    {
        var before = ProductWithImages(beforeKeys.Select(key =>
            new ProductImageDefinition(
                Guid.NewGuid(), key, $"/media/{key}", "ภาพเดิม")));
        var after = ProductWithImages(afterMedia.Select(media =>
            new ProductImageDefinition(
                Guid.NewGuid(), media.StorageKey, media.PublicRelativeUrl, "ภาพใหม่")));
        return ProductMediaMutationState.Capture(
            ProductMediaSnapshot.Capture(before.Images),
            after.Images);
    }

    private static Product ProductWithImages(IEnumerable<ProductImageDefinition> images) =>
        Product.CreateInStock(
            Guid.NewGuid(), "สินค้า", "Product", "รายละเอียด", "product",
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            InStockOffer.Create(Money.Create(100)),
            images.ToArray(), [],
            new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero),
            "test");

    private static Task<CatalogCommitVerification<string>> Verification(
        CatalogCommitVerification outcome,
        string authoritative = "authoritative") =>
        Task.FromResult(outcome switch
        {
            CatalogCommitVerification.Committed =>
                CatalogCommitVerificationResult.Committed(authoritative),
            CatalogCommitVerification.Superseded =>
                CatalogCommitVerificationResult.Superseded(authoritative),
            CatalogCommitVerification.NotCommitted =>
                CatalogCommitVerificationResult.NotCommitted<string>(),
            CatalogCommitVerification.Unavailable =>
                CatalogCommitVerificationResult.Unavailable<string>(),
            CatalogCommitVerification.Inconsistent =>
                CatalogCommitVerificationResult.Inconsistent<string>(),
            _ => throw new InvalidOperationException("Unknown verification outcome."),
        });

    private sealed class CoordinatorFixture
    {
        public CoordinatorFixture(ProductMediaMutationCoordinator coordinator) =>
            Inner = coordinator;

        public ProductMediaMutationCoordinator Inner { get; }

        public Task<Result<T>> ExecuteAsync<T, TAuthoritative>(
            IReadOnlyList<MediaUpload> uploads,
            ICatalogMutationSession session,
            Func<IReadOnlyList<StagedMedia>, CancellationToken, Task<Result<T>>> mutation,
            Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
            Func<TAuthoritative, T> refreshResult,
            ProductMediaMutationContext context,
            Func<IReadOnlyCollection<string>> removedOldKeysAccessor,
            CancellationToken cancellationToken)
        {
            ProductMediaMutationState? state = null;
            var plan = uploads
                .Select(upload => (ProductMediaPlanSlot)new UploadProductMediaSlot(upload))
                .ToArray();
            return Inner.ExecuteAsync(
                plan,
                session,
                async (resolved, token) =>
                {
                    var staged = resolved
                        .OfType<ResolvedUploadProductMediaSlot>()
                        .Select(slot => slot.Media)
                        .ToArray();
                    var result = await mutation(staged, token);
                    if (result.IsSuccess)
                    {
                        state = State(removedOldKeysAccessor(), staged);
                    }

                    return result;
                },
                verifyCommit,
                refreshResult,
                context,
                () => state ?? throw new InvalidOperationException("Missing Product media state."),
                cancellationToken);
        }
    }

    private sealed class FakeSession(
        CatalogCommitOutcome outcome,
        ConcurrentQueue<string>? events = null) : ICatalogMutationSession
    {
        private bool executed;

        public int ExecutionCount { get; private set; }
        public int CallbackCount { get; private set; }
        public Exception? ExceptionAfterOperation { get; init; }
        public Exception? CommitFailureException { get; init; }

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            if (executed)
            {
                throw new InvalidOperationException("Session is once-only.");
            }

            executed = true;
            ExecutionCount++;
            CallbackCount++;
            var result = await operation(cancellationToken);
            events?.Enqueue("database-save");
            if (ExceptionAfterOperation is not null)
            {
                throw ExceptionAfterOperation;
            }

            return new CatalogMutationExecution<T>(
                result,
                outcome,
                outcome == CatalogCommitOutcome.Indeterminate
                    ? CatalogCommitFailure.Create(
                        CommitFailureException ?? new IOException("commit acknowledgement lost"))
                    : null);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeStorage(ConcurrentQueue<string>? events = null) : IFileStorage
    {
        public int StageCount { get; private set; }
        public int CommitCount { get; private set; }
        public int? ReturnedMediaCount { get; init; }
        public bool WrongBatchToken { get; init; }
        public bool DuplicateKey { get; init; }
        public bool NullMedia { get; init; }
        public bool InvalidStorageKey { get; init; }
        public Action? BeforeStage { get; set; }
        public int LastUploadCount { get; private set; }
        public Error? StageError { get; init; }
        public Exception? StageException { get; init; }
        public Exception? CommitException { get; init; }
        public Exception? DeleteException { get; init; }
        public List<string> DeletedKeys { get; } = [];
        public List<CancellationToken> DeleteTokens { get; } = [];
        public List<CancellationToken> DiscardTokens { get; } = [];

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            StageCount++;
            BeforeStage?.Invoke();
            LastUploadCount = uploads.Count;
            if (StageException is not null)
            {
                return Task.FromException<Result<StagedMediaBatch>>(StageException);
            }

            if (StageError is not null)
            {
                return Task.FromResult(Result<StagedMediaBatch>.Failure(StageError));
            }

            var count = ReturnedMediaCount ?? uploads.Count;
            var media = Enumerable.Range(1, count)
                .Select(index => new StagedMedia(
                    WrongBatchToken && index == count ? new string('b', 32) : BatchToken,
                    InvalidStorageKey ? "../invalid.webp" : MediaKey(DuplicateKey ? 1 : index),
                    InvalidStorageKey
                        ? "/media/../invalid.webp"
                        : $"/media/{MediaKey(DuplicateKey ? 1 : index)}",
                    "image/webp", 3))
                .Cast<StagedMedia?>()
                .ToArray();
            if (NullMedia && media.Length > 0)
            {
                media[^1] = null;
            }

            return Task.FromResult(Result<StagedMediaBatch>.Success(
                new StagedMediaBatch(BatchToken, media!)));
        }

        public Task CommitAsync(StagedMediaBatch batch, CancellationToken cancellationToken)
        {
            CommitCount++;
            events?.Enqueue("media-commit");
            return CommitException is null
                ? Task.CompletedTask
                : Task.FromException(CommitException);
        }

        public Task DiscardStagingAsync(string batchToken, CancellationToken cancellationToken)
        {
            DiscardTokens.Add(cancellationToken);
            return Task.CompletedTask;
        }

        public Task DeleteCommittedAsync(
            IReadOnlyCollection<string> storageKeys,
            CancellationToken cancellationToken)
        {
            DeletedKeys.AddRange(storageKeys);
            DeleteTokens.Add(cancellationToken);
            events?.Enqueue("media-delete");
            return DeleteException is null
                ? Task.CompletedTask
                : Task.FromException(DeleteException);
        }

        public Task<StoredMediaRead?> OpenReadAsync(
            string storageKey,
            CancellationToken cancellationToken) => Task.FromResult<StoredMediaRead?>(null);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class FakeVerifier : IMediaReferenceVerifier
    {
        private readonly IReadOnlyDictionary<string, MediaReferenceVerification>? byKey;
        private readonly MediaReferenceVerification fallback;

        public FakeVerifier(IReadOnlyDictionary<string, MediaReferenceVerification> byKey)
        {
            this.byKey = byKey;
            fallback = MediaReferenceVerification.Unavailable;
        }

        private FakeVerifier(MediaReferenceVerification fallback) => this.fallback = fallback;

        public static FakeVerifier All(MediaReferenceVerification verification) => new(verification);

        public Task<MediaReferenceVerification> VerifyAsync(
            TrustedMediaStorageKey storageKey,
            CancellationToken cancellationToken) =>
            Task.FromResult(byKey is not null && byKey.TryGetValue(storageKey.Value, out var value)
                ? value
                : fallback);
    }

    private sealed class FakeRegistry : IMediaCleanupRegistry
    {
        public List<MediaCleanupRegistration> Registrations { get; } = [];
        public int AttemptCount { get; private set; }
        public Exception? Exception { get; init; }

        public Task RecordAsync(
            MediaCleanupRegistration registration,
            CancellationToken cancellationToken)
        {
            AttemptCount++;
            if (Exception is not null)
            {
                return Task.FromException(Exception);
            }

            Registrations.Add(registration);
            return Task.CompletedTask;
        }
    }
}
