using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application;

public sealed class MediaMutationCoordinatorTests
{
    private static readonly MediaMutationContext Context = new(
        "Brand",
        Guid.Parse("01392e38-a8eb-4890-bc87-247bff513841"),
        null);

    [Fact]
    public async Task StagesOnceAndCommitsMediaBeforeTheOperationOwnedSessionSaves()
    {
        var events = new ConcurrentQueue<string>();
        var storage = new FakeFileStorage(events);
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed, events);
        var coordinator = CreateCoordinator(storage);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            (media, _) => Task.FromResult(Result<string>.Success(media.StorageKey)),
            _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(1, storage.StageCount);
        Assert.Equal(1, storage.CommitCount);
        Assert.Equal(1, session.ExecutionCount);
        Assert.True(
            Array.IndexOf(events.ToArray(), "media-commit")
            < Array.IndexOf(events.ToArray(), "database-save"));
    }

    [Fact]
    public async Task ReplacementResolvesLockedPreviousMediaAfterMutationBeforeCleanup()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Unreferenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed);
        var coordinator = CreateCoordinator(storage, verifier);
        CatalogMediaReference? lockedPreviousMedia = null;
        var previous = CatalogMediaReference.Create(
            "brands/previous.webp",
            "/media/brands/previous.webp",
            "รูปเดิม");

        var result = await coordinator.ExecuteAsync<string, string>(
            Upload(),
            session,
            (media, _) =>
            {
                lockedPreviousMedia = previous;
                return Task.FromResult(Result<string>.Success(media.StorageKey));
            },
            _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
            static value => value,
            Context,
            () => lockedPreviousMedia,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([previous.StorageKey], storage.DeletedKeys);
        Assert.Equal(2, verifier.CallCount);
    }

    [Fact]
    public async Task CommitAcknowledgementLostThenNewerArchiveStillReferencesKeyRetainsMedia()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(MediaReferenceVerification.Referenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage, verifier);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Superseded, "authoritative-archive"),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal("authoritative-archive", result.Value);
        Assert.Empty(storage.DeletedKeys);
        Assert.Empty(coordinator.Registry.Registrations);
        Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain(storage.StorageKey, coordinator.Logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task CommitAcknowledgementLostThenNewerUpdateNoLongerReferencesKeyDeletesAfterRepeatGuard()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Unreferenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage, verifier);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Superseded, "authoritative-update"),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal([storage.StorageKey], storage.DeletedKeys);
        Assert.Equal(2, verifier.CallCount);
        Assert.All(storage.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task IndeterminateCommitConfirmedAbsentCompensatesButReturnsSafeFailure()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Unreferenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage, verifier);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Persistence.CommitOutcomeUnknown", result.Error.Code);
        Assert.Equal([storage.StorageKey], storage.DeletedKeys);
    }

    [Fact]
    public async Task UnavailableFreshVerificationRetainsMediaAndRecordsCleanup()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(MediaReferenceVerification.Unavailable);
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage, verifier);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Unavailable),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("Persistence.CommitOutcomeUnknown", result.Error.Code);
        Assert.Empty(storage.DeletedKeys);
        var registration = Assert.Single(coordinator.Registry.Registrations);
        Assert.Equal(storage.StorageKey, registration.StorageKey.Value);
        Assert.Equal(MediaCleanupReason.CommitOutcomeUnknown, registration.Reason);
        var logMessage = Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain(storage.StorageKey, logMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task InconsistentFreshVerificationRetainsMediaAndRecordsCleanup()
    {
        var storage = new FakeFileStorage();
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Inconsistent),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Empty(storage.DeletedKeys);
        Assert.Equal(
            MediaCleanupReason.CommitOutcomeUnknown,
            Assert.Single(coordinator.Registry.Registrations).Reason);
        Assert.Single(coordinator.Logger.Messages);
    }

    [Fact]
    public async Task CommitCancellationReconcilesCommittedMediaThenRethrowsOriginalCancellation()
    {
        var storage = new FakeFileStorage();
        var cancellation = new OperationCanceledException("commit cancelled");
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate)
        {
            CommitException = cancellation,
        };
        var coordinator = CreateCoordinator(storage);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
            Context,
            TestContext.Current.CancellationToken));

        Assert.Same(cancellation, thrown);
        Assert.Empty(storage.DeletedKeys);
        Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain(LogLevel.Error, coordinator.Logger.Levels);
        Assert.DoesNotContain("commit cancelled", coordinator.Logger.Messages[0], StringComparison.Ordinal);
    }

    [Fact]
    public async Task RepeatGuardSeesNewReferenceAndRefusesCompensationDelete()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Referenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Indeterminate);
        var coordinator = CreateCoordinator(storage, verifier);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Empty(storage.DeletedKeys);
        Assert.Equal(2, verifier.CallCount);
    }

    [Fact]
    public async Task CancellationDuringMediaCommitUsesNonCancellableCompensation()
    {
        var storage = new FakeFileStorage
        {
            CommitException = new OperationCanceledException(),
        };
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Unreferenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed);
        var coordinator = CreateCoordinator(storage, verifier);

        await Assert.ThrowsAsync<OperationCanceledException>(() => coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context,
            TestContext.Current.CancellationToken));

        Assert.Single(storage.DiscardTokens);
        Assert.False(storage.DiscardTokens[0].CanBeCanceled);
        Assert.Equal([storage.StorageKey], storage.DeletedKeys);
        Assert.All(storage.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task TypedFailureDefinitelyRollsBackWithoutCommittingMedia()
    {
        var storage = new FakeFileStorage();
        var session = new FakeMutationSession(CatalogCommitOutcome.DefinitelyRolledBack);
        var coordinator = CreateCoordinator(storage);
        var businessError = new Error(
            "Brand.DuplicateName",
            "ชื่อแบรนด์นี้มีอยู่แล้ว",
            ErrorType.Conflict);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            (_, _) => Task.FromResult(Result<string>.Failure(businessError)),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(businessError, result.Error);
        Assert.Equal(0, storage.CommitCount);
        Assert.Empty(storage.DeletedKeys);
        Assert.Single(storage.DiscardTokens);
        Assert.False(storage.DiscardTokens[0].CanBeCanceled);
    }

    [Fact]
    public async Task TypedFailurePreservesResultAndLogsDiscardFailureOnceWithoutSensitiveData()
    {
        const string sensitiveValue = "batch-token-or-customer@example.com";
        var storage = new FakeFileStorage
        {
            DiscardException = new IOException(sensitiveValue),
        };
        var session = new FakeMutationSession(CatalogCommitOutcome.DefinitelyRolledBack);
        var coordinator = CreateCoordinator(storage);
        var businessError = new Error(
            "Brand.DuplicateName",
            "ชื่อแบรนด์นี้มีอยู่แล้ว",
            ErrorType.Conflict);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            (_, _) => Task.FromResult(Result<string>.Failure(businessError)),
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal(businessError, result.Error);
        Assert.Equal([LogLevel.Error], coordinator.Logger.Levels);
        var message = Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain(sensitiveValue, message, StringComparison.Ordinal);
        Assert.DoesNotContain("batch-1", message, StringComparison.Ordinal);
        Assert.DoesNotContain(storage.StorageKey, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationPreservesOriginalExceptionAndLogsDiscardFailureOnceWithoutSensitiveData()
    {
        const string sensitiveValue = "batch-token-or-customer@example.com";
        var cancellation = new OperationCanceledException("original cancellation");
        var storage = new FakeFileStorage
        {
            DiscardException = new IOException(sensitiveValue),
        };
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed)
        {
            ExceptionAfterOperation = cancellation,
        };
        var coordinator = CreateCoordinator(storage);

        var actual = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            coordinator.ExecuteAsync(
                Upload(),
                session,
                (_, _) => throw cancellation,
                _ => Verification(CatalogCommitVerification.NotCommitted),
                Context,
                TestContext.Current.CancellationToken));

        Assert.Same(cancellation, actual);
        Assert.Equal([LogLevel.Error], coordinator.Logger.Levels);
        var message = Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain(sensitiveValue, message, StringComparison.Ordinal);
        Assert.DoesNotContain("batch-1", message, StringComparison.Ordinal);
        Assert.DoesNotContain(storage.StorageKey, message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveFailureAfterMediaCommitDeletesOnlyNewUnreferencedMedia()
    {
        var storage = new FakeFileStorage();
        var verifier = new FakeMediaReferenceVerifier(
            MediaReferenceVerification.Unreferenced,
            MediaReferenceVerification.Unreferenced);
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed)
        {
            ExceptionAfterOperation = new InvalidOperationException("database save failed"),
        };
        var previousMedia = CatalogMediaReference.Create(
            "catalog/previous.webp",
            "/media/catalog/previous.webp",
            "แบรนด์เดิม");
        var coordinator = CreateCoordinator(storage, verifier);

        await Assert.ThrowsAsync<InvalidOperationException>(() => coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.NotCommitted),
            Context with { PreviousMedia = previousMedia },
            TestContext.Current.CancellationToken));

        Assert.Equal([storage.StorageKey], storage.DeletedKeys);
        Assert.DoesNotContain(previousMedia.StorageKey, storage.DeletedKeys);
        Assert.All(storage.DeleteTokens, token => Assert.False(token.CanBeCanceled));
    }

    [Fact]
    public async Task OverlappingMutationsKeepStageAndSessionStateIsolated()
    {
        var storage = new FakeFileStorage();
        var coordinator = CreateCoordinator(storage);
        var firstSession = new FakeMutationSession(CatalogCommitOutcome.Committed);
        var secondSession = new FakeMutationSession(CatalogCommitOutcome.Committed);

        var results = await Task.WhenAll(
            coordinator.ExecuteAsync(
                Upload(),
                firstSession,
                SuccessMutation,
                _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
                Context,
                TestContext.Current.CancellationToken),
            coordinator.ExecuteAsync(
                Upload(),
                secondSession,
                SuccessMutation,
                _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
                Context with { EntityId = Guid.NewGuid() },
                TestContext.Current.CancellationToken));

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, storage.StageCount);
        Assert.Equal(2, storage.CommitCount);
        Assert.Equal(2, storage.CommittedBatchTokens.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(1, firstSession.ExecutionCount);
        Assert.Equal(1, secondSession.ExecutionCount);
    }

    [Fact]
    public async Task SessionContractRejectsSecondExecution()
    {
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed);

        await session.ExecuteOnceAsync(
            _ => Task.FromResult(Result<string>.Success("first")),
            TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => session.ExecuteOnceAsync(
            _ => Task.FromResult(Result<string>.Success("second")),
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DurableCommitCleanupFailureIsLoggedOnceWithoutChangingSuccess()
    {
        var storage = new FakeFileStorage();
        var session = new FakeMutationSession(CatalogCommitOutcome.Committed)
        {
            CleanupFailureTypes = ["StorageKey=must-not-appear"],
        };
        var coordinator = CreateCoordinator(storage);

        var result = await coordinator.ExecuteAsync(
            Upload(),
            session,
            SuccessMutation,
            _ => Verification(CatalogCommitVerification.Committed, storage.StorageKey),
            Context,
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        var message = Assert.Single(coordinator.Logger.Messages);
        Assert.DoesNotContain("StorageKey=must-not-appear", message, StringComparison.Ordinal);
    }

    private static MediaUpload Upload() =>
        new(new MemoryStream([0xff, 0xd8, 0xff]), "image/jpeg");

    private static Task<Result<string>> SuccessMutation(
        StagedMedia media,
        CancellationToken _) =>
        Task.FromResult(Result<string>.Success(media.StorageKey));

    private static Task<CatalogCommitVerification<string>> Verification(
        CatalogCommitVerification outcome,
        string authoritativeState = "authoritative") =>
        Task.FromResult(outcome switch
        {
            CatalogCommitVerification.Committed =>
                CatalogCommitVerificationResult.Committed(authoritativeState),
            CatalogCommitVerification.Superseded =>
                CatalogCommitVerificationResult.Superseded(authoritativeState),
            CatalogCommitVerification.NotCommitted =>
                CatalogCommitVerificationResult.NotCommitted<string>(),
            CatalogCommitVerification.Unavailable =>
                CatalogCommitVerificationResult.Unavailable<string>(),
            CatalogCommitVerification.Inconsistent =>
                CatalogCommitVerificationResult.Inconsistent<string>(),
            _ => throw new InvalidOperationException("Unknown verification outcome."),
        });

    private static CoordinatorFixture CreateCoordinator(
        FakeFileStorage storage,
        FakeMediaReferenceVerifier? verifier = null)
    {
        var registry = new FakeMediaCleanupRegistry();
        var logger = new CapturingLogger<MediaMutationCoordinator>();
        return new CoordinatorFixture(
            new MediaMutationCoordinator(
                storage,
                verifier ?? new FakeMediaReferenceVerifier(MediaReferenceVerification.Referenced),
                registry,
                logger),
            registry,
            logger);
    }

    private sealed class CoordinatorFixture(
        MediaMutationCoordinator coordinator,
        FakeMediaCleanupRegistry registry,
        CapturingLogger<MediaMutationCoordinator> logger)
    {
        public FakeMediaCleanupRegistry Registry { get; } = registry;

        public CapturingLogger<MediaMutationCoordinator> Logger { get; } = logger;

        public Task<Result<T>> ExecuteAsync<T>(
            MediaUpload upload,
            ICatalogMutationSession session,
            Func<StagedMedia, CancellationToken, Task<Result<T>>> mutation,
            Func<CancellationToken, Task<CatalogCommitVerification<T>>> verifyCommit,
            MediaMutationContext context,
            CancellationToken cancellationToken) =>
            coordinator.ExecuteAsync(
                upload,
                session,
                mutation,
                verifyCommit,
                context,
                cancellationToken);

        public Task<Result<T>> ExecuteAsync<T, TAuthoritative>(
            MediaUpload upload,
            ICatalogMutationSession session,
            Func<StagedMedia, CancellationToken, Task<Result<T>>> mutation,
            Func<CancellationToken, Task<CatalogCommitVerification<TAuthoritative>>> verifyCommit,
            Func<TAuthoritative, T> refreshResult,
            MediaMutationContext context,
            Func<CatalogMediaReference?> previousMediaAccessor,
            CancellationToken cancellationToken) =>
            coordinator.ExecuteAsync(
                upload,
                session,
                mutation,
                verifyCommit,
                refreshResult,
                context,
                previousMediaAccessor,
                cancellationToken);
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Levels.Add(logLevel);
            Messages.Add(formatter(state, exception));
        }
    }

    private sealed class FakeMutationSession(
        CatalogCommitOutcome outcome,
        ConcurrentQueue<string>? events = null) : ICatalogMutationSession
    {
        private bool executed;

        public int ExecutionCount { get; private set; }

        public Exception? ExceptionAfterOperation { get; init; }

        public Exception? CommitException { get; init; }

        public IReadOnlyList<string> CleanupFailureTypes { get; init; } = [];

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            if (executed)
            {
                throw new InvalidOperationException("Session execution is once-only.");
            }

            executed = true;
            ExecutionCount++;
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
                        CommitException ?? new InvalidOperationException("commit acknowledgement lost"))
                    : null,
                CleanupFailureTypes);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeMediaReferenceVerifier(
        params MediaReferenceVerification[] responses) : IMediaReferenceVerifier
    {
        private readonly ConcurrentQueue<MediaReferenceVerification> responses = new(responses);

        public int CallCount { get; private set; }

        public Task<MediaReferenceVerification> VerifyAsync(
            TrustedMediaStorageKey storageKey,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(this.responses.TryDequeue(out var response)
                ? response
                : MediaReferenceVerification.Unavailable);
        }
    }

    private sealed class FakeMediaCleanupRegistry : IMediaCleanupRegistry
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

    private sealed class FakeFileStorage(ConcurrentQueue<string>? events = null) : IFileStorage
    {
        private int sequence;

        public string StorageKey { get; private set; } = string.Empty;

        public int StageCount { get; private set; }

        public int CommitCount { get; private set; }

        public Exception? CommitException { get; init; }

        public Exception? DiscardException { get; init; }

        public List<string> DeletedKeys { get; } = [];

        public List<CancellationToken> DeleteTokens { get; } = [];

        public List<CancellationToken> DiscardTokens { get; } = [];

        public List<string> CommittedBatchTokens { get; } = [];

        public Task<Result<StagedMediaBatch>> StageAsync(
            IReadOnlyCollection<MediaUpload> uploads,
            CancellationToken cancellationToken)
        {
            var current = Interlocked.Increment(ref sequence);
            var token = $"batch-{current}";
            var key = $"catalog/{current}.webp";
            StorageKey = key;
            StageCount++;
            events?.Enqueue("media-stage");
            return Task.FromResult(Result<StagedMediaBatch>.Success(new StagedMediaBatch(
                token,
                [new StagedMedia(token, key, $"/media/{key}", "image/webp", 3)])));
        }

        public Task CommitAsync(
            StagedMediaBatch batch,
            CancellationToken cancellationToken)
        {
            CommitCount++;
            CommittedBatchTokens.Add(batch.BatchToken);
            events?.Enqueue("media-commit");
            return CommitException is null
                ? Task.CompletedTask
                : Task.FromException(CommitException);
        }

        public Task DiscardStagingAsync(
            string batchToken,
            CancellationToken cancellationToken)
        {
            DiscardTokens.Add(cancellationToken);
            return DiscardException is null
                ? Task.CompletedTask
                : Task.FromException(DiscardException);
        }

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
            CancellationToken cancellationToken) =>
            Task.FromResult<StoredMediaRead?>(null);

        public Task CleanupStagingAsync(
            DateTimeOffset olderThanUtc,
            CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }
}
