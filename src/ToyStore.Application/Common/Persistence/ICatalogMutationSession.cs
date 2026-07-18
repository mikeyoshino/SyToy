using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Common.Persistence;

public interface ICatalogMutationSession : IAsyncDisposable
{
    Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken);
}

public enum CatalogCommitOutcome
{
    Committed = 1,
    DefinitelyRolledBack = 2,
    Indeterminate = 3,
}

public enum CatalogCommitVerification
{
    Committed = 1,
    Superseded = 2,
    NotCommitted = 3,
    Unavailable = 4,
    Inconsistent = 5,
}

public sealed class CatalogCommitVerification<TAuthoritative>
{
    private readonly TAuthoritative? authoritativeState;

    internal CatalogCommitVerification(
        CatalogCommitVerification outcome,
        TAuthoritative? authoritativeState,
        bool hasAuthoritativeState)
    {
        Outcome = outcome;
        this.authoritativeState = authoritativeState;
        HasAuthoritativeState = hasAuthoritativeState;
    }

    public CatalogCommitVerification Outcome { get; }

    public bool HasAuthoritativeState { get; }

    public TAuthoritative AuthoritativeState => HasAuthoritativeState
        ? authoritativeState!
        : throw new InvalidOperationException(
            "This commit verification does not carry authoritative state.");

}

public static class CatalogCommitVerificationResult
{
    public static CatalogCommitVerification<TAuthoritative> Committed<TAuthoritative>(
        TAuthoritative authoritativeState) =>
        WithAuthoritativeState(CatalogCommitVerification.Committed, authoritativeState);

    public static CatalogCommitVerification<TAuthoritative> Superseded<TAuthoritative>(
        TAuthoritative authoritativeState) =>
        WithAuthoritativeState(CatalogCommitVerification.Superseded, authoritativeState);

    public static CatalogCommitVerification<TAuthoritative> NotCommitted<TAuthoritative>() =>
        WithoutAuthoritativeState<TAuthoritative>(CatalogCommitVerification.NotCommitted);

    public static CatalogCommitVerification<TAuthoritative> Unavailable<TAuthoritative>() =>
        WithoutAuthoritativeState<TAuthoritative>(CatalogCommitVerification.Unavailable);

    public static CatalogCommitVerification<TAuthoritative> Inconsistent<TAuthoritative>() =>
        WithoutAuthoritativeState<TAuthoritative>(CatalogCommitVerification.Inconsistent);

    private static CatalogCommitVerification<TAuthoritative> WithAuthoritativeState<TAuthoritative>(
        CatalogCommitVerification outcome,
        TAuthoritative authoritativeState)
    {
        ArgumentNullException.ThrowIfNull(authoritativeState);
        return new CatalogCommitVerification<TAuthoritative>(outcome, authoritativeState, true);
    }

    private static CatalogCommitVerification<TAuthoritative> WithoutAuthoritativeState<TAuthoritative>(
        CatalogCommitVerification outcome) =>
        new(outcome, default, false);
}

public sealed record CatalogCommitFailure(
    Exception OriginalException,
    IReadOnlyList<string> CleanupFailureTypes)
{
    public bool IsCancellation => OriginalException is OperationCanceledException;

    public static CatalogCommitFailure Create(
        Exception originalException,
        IEnumerable<Exception>? cleanupExceptions = null)
    {
        ArgumentNullException.ThrowIfNull(originalException);
        return new CatalogCommitFailure(
            originalException,
            (cleanupExceptions ?? [])
                .Select(exception => exception.GetType().FullName ?? exception.GetType().Name)
                .ToArray());
    }
}

public sealed record CatalogMutationExecution<T>(
    Result<T> Result,
    CatalogCommitOutcome CommitOutcome,
    CatalogCommitFailure? CommitFailure = null,
    IReadOnlyList<string>? CleanupFailureTypes = null)
{
    public IReadOnlyList<string> SafeCleanupFailureTypes => CleanupFailureTypes ?? [];
}
