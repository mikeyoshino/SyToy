using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.UnitTests.Application;

public sealed class CatalogCommitOutcomeResolverTests
{
    [Fact]
    public async Task CommittedAndDefinitelyRolledBackResultsPassThroughWithoutVerification()
    {
        var resolver = new CatalogCommitOutcomeResolver(new ListLogger());
        var committed = new CatalogMutationExecution<string>(
            Result<string>.Success("saved"),
            CatalogCommitOutcome.Committed);
        var rolledBack = new CatalogMutationExecution<string>(
            Result<string>.Failure(TestError),
            CatalogCommitOutcome.DefinitelyRolledBack);
        var verifyCount = 0;

        Task<CatalogCommitVerification<string>> Verify(CancellationToken _)
        {
            verifyCount++;
            return Task.FromResult(CatalogCommitVerificationResult.Committed("fresh"));
        }

        var committedResult = await resolver.ResolveAsync(
            committed, Verify, static value => value, "Brand", CancellationToken.None);
        var rolledBackResult = await resolver.ResolveAsync(
            rolledBack, Verify, static value => value, "Brand", CancellationToken.None);

        Assert.Equal("saved", committedResult.Value);
        Assert.Equal(TestError, rolledBackResult.Error);
        Assert.Equal(0, verifyCount);
    }

    [Fact]
    public async Task CommittedCleanupMetadataLogsOnceWithoutChangingSuccess()
    {
        var logger = new ListLogger();
        var resolver = new CatalogCommitOutcomeResolver(logger);
        var execution = new CatalogMutationExecution<string>(
            Result<string>.Success("saved"),
            CatalogCommitOutcome.Committed,
            CleanupFailureTypes: ["StorageKey=must-not-appear"]);

        var result = await resolver.ResolveAsync<string, string>(
            execution,
            _ => throw new InvalidOperationException("verification must not run"),
            static value => value,
            "Brand",
            CancellationToken.None);

        Assert.Equal("saved", result.Value);
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        var message = Assert.Single(logger.Messages);
        Assert.Contains("cleanup failures: 1", message, StringComparison.Ordinal);
        Assert.DoesNotContain("must-not-appear", message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task TypedRollbackCleanupMetadataLogsOnceWithoutChangingFailure()
    {
        var logger = new ListLogger();
        var resolver = new CatalogCommitOutcomeResolver(logger);
        var execution = new CatalogMutationExecution<string>(
            Result<string>.Failure(TestError),
            CatalogCommitOutcome.DefinitelyRolledBack,
            CleanupFailureTypes: ["secret cleanup detail"]);

        var result = await resolver.ResolveAsync<string, string>(
            execution,
            _ => throw new InvalidOperationException("verification must not run"),
            static value => value,
            "Brand",
            CancellationToken.None);

        Assert.Equal(TestError, result.Error);
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        Assert.DoesNotContain(
            "secret cleanup detail",
            Assert.Single(logger.Messages),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task IndeterminateSupersededUsesFreshAuthoritativeStateAndNonCancellableVerification()
    {
        var logger = new ListLogger();
        var resolver = new CatalogCommitOutcomeResolver(logger);
        var execution = Indeterminate(new InjectedCommitException());
        var receivedToken = new CancellationToken(canceled: true);

        var result = await resolver.ResolveAsync(
            execution,
            cancellationToken =>
            {
                receivedToken = cancellationToken;
                return Task.FromResult(CatalogCommitVerificationResult.Superseded("fresh"));
            },
            static value => $"authoritative-{value}",
            "Brand",
            new CancellationToken(canceled: true));

        Assert.Equal("authoritative-fresh", result.Value);
        Assert.False(receivedToken.CanBeCanceled);
        Assert.Equal(LogLevel.Error, Assert.Single(logger.Levels));
        Assert.DoesNotContain("secret", Assert.Single(logger.Messages), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CatalogCommitVerification.NotCommitted)]
    [InlineData(CatalogCommitVerification.Unavailable)]
    [InlineData(CatalogCommitVerification.Inconsistent)]
    public async Task UnconfirmedCommitReturnsSafeRefreshBeforeRetryFailure(
        CatalogCommitVerification verification)
    {
        var resolver = new CatalogCommitOutcomeResolver(new ListLogger());

        var result = await resolver.ResolveAsync(
            Indeterminate(new InjectedCommitException()),
            _ => Task.FromResult(Verification(verification)),
            static value => value,
            "Brand",
            TestContext.Current.CancellationToken);

        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, result.Error);
    }

    [Fact]
    public async Task CommitCancellationReconcilesThenRethrowsSameCancellationAtInformation()
    {
        var logger = new ListLogger();
        var resolver = new CatalogCommitOutcomeResolver(logger);
        var cancellation = new OperationCanceledException("secret cancellation");

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() => resolver.ResolveAsync(
            Indeterminate(cancellation),
            _ => Task.FromResult(CatalogCommitVerificationResult.Committed("fresh")),
            static value => value,
            "Brand",
            CancellationToken.None));

        Assert.Same(cancellation, thrown);
        Assert.Equal(LogLevel.Information, Assert.Single(logger.Levels));
        Assert.DoesNotContain("secret cancellation", Assert.Single(logger.Messages), StringComparison.Ordinal);
    }

    private static readonly Error TestError = new(
        "Brand.Test",
        "ทดสอบ",
        ErrorType.Conflict);

    private static CatalogMutationExecution<string> Indeterminate(Exception exception) =>
        new(
            Result<string>.Success("intended"),
            CatalogCommitOutcome.Indeterminate,
            CatalogCommitFailure.Create(exception));

    private static CatalogCommitVerification<string> Verification(
        CatalogCommitVerification outcome) => outcome switch
        {
            CatalogCommitVerification.NotCommitted =>
                CatalogCommitVerificationResult.NotCommitted<string>(),
            CatalogCommitVerification.Unavailable =>
                CatalogCommitVerificationResult.Unavailable<string>(),
            CatalogCommitVerification.Inconsistent =>
                CatalogCommitVerificationResult.Inconsistent<string>(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };

    private sealed class InjectedCommitException : Exception
    {
        public InjectedCommitException()
            : base("secret commit details")
        {
        }
    }

    private sealed class ListLogger : ILogger<CatalogCommitOutcomeResolver>
    {
        public List<LogLevel> Levels { get; } = [];

        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
}
