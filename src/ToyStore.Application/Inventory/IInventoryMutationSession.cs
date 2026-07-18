using ToyStore.Application.Common.Models;
using ToyStore.Domain.Inventory;

namespace ToyStore.Application.Inventory;

public interface IInventoryMutationSessionFactory
{
    ValueTask<IInventoryMutationSession> OpenAsync(CancellationToken cancellationToken);

    Task<InventoryCommitVerificationResult> VerifyCommitAsync(
        InventoryMutationEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IInventoryMutationSession : IAsyncDisposable
{
    Task<InventoryMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken);

    Task<InventoryItem?> LockInventoryAsync(
        Guid inventoryItemId,
        Guid productId,
        CancellationToken cancellationToken);

    Task<StockMovement?> FindMovementAsync(
        Guid operationId,
        CancellationToken cancellationToken);

    Task<StockReservation?> FindReservationAsync(
        Guid reservationId,
        CancellationToken cancellationToken);

    void Add(InventoryCreation creation);

    void Add(StockMovement movement);

    void Add(StockReservation reservation);
}

public enum InventoryCommitOutcome
{
    Committed = 1,
    DefinitelyRolledBack = 2,
    Indeterminate = 3,
}

public enum InventoryCommitVerification
{
    Committed = 1,
    Superseded = 2,
    Conflict = 3,
    Inconsistent = 4,
    Unavailable = 5,
}

public sealed record InventoryCommitFailure(
    Exception OriginalException,
    IReadOnlyList<string> CleanupFailureTypes)
{
    public bool IsCancellation => OriginalException is OperationCanceledException;

    public static InventoryCommitFailure Create(
        Exception originalException,
        IEnumerable<Exception>? cleanupExceptions = null)
    {
        ArgumentNullException.ThrowIfNull(originalException);
        return new InventoryCommitFailure(
            originalException,
            (cleanupExceptions ?? [])
                .Select(exception => exception.GetType().FullName ?? exception.GetType().Name)
                .ToArray());
    }
}

public sealed class InventoryMutationExecution<T>
{
    public InventoryMutationExecution(
        Result<T> result,
        InventoryCommitOutcome commitOutcome,
        InventoryCommitFailure? commitFailure = null,
        IReadOnlyList<string>? cleanupFailureTypes = null)
    {
        Result = result;
        CommitOutcome = commitOutcome;
        CommitFailure = commitFailure;
        CleanupFailureTypes = cleanupFailureTypes?.ToArray();
    }

    public Result<T> Result { get; }

    public InventoryCommitOutcome CommitOutcome { get; }

    public InventoryCommitFailure? CommitFailure { get; }

    public IReadOnlyList<string>? CleanupFailureTypes { get; }

    public IReadOnlyList<string> SafeCleanupFailureTypes => CleanupFailureTypes ?? [];
}

public sealed class InventoryCommitVerificationResult
{
    private readonly InventoryMutationEvidence? authoritativeEvidence;

    private InventoryCommitVerificationResult(
        InventoryCommitVerification outcome,
        InventoryMutationEvidence? authoritativeEvidence)
    {
        Outcome = outcome;
        this.authoritativeEvidence = authoritativeEvidence;
    }

    public InventoryCommitVerification Outcome { get; }

    public bool HasAuthoritativeEvidence => authoritativeEvidence is not null;

    public InventoryMutationEvidence AuthoritativeEvidence =>
        authoritativeEvidence
        ?? throw new InvalidOperationException(
            "This verification result has no authoritative Inventory evidence.");

    public static InventoryCommitVerificationResult Committed(
        InventoryMutationEvidence evidence) =>
        WithEvidence(InventoryCommitVerification.Committed, evidence);

    public static InventoryCommitVerificationResult Superseded(
        InventoryMutationEvidence evidence) =>
        WithEvidence(InventoryCommitVerification.Superseded, evidence);

    public static InventoryCommitVerificationResult Conflict() =>
        new(InventoryCommitVerification.Conflict, null);

    public static InventoryCommitVerificationResult Inconsistent() =>
        new(InventoryCommitVerification.Inconsistent, null);

    public static InventoryCommitVerificationResult Unavailable() =>
        new(InventoryCommitVerification.Unavailable, null);

    private static InventoryCommitVerificationResult WithEvidence(
        InventoryCommitVerification outcome,
        InventoryMutationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new InventoryCommitVerificationResult(outcome, evidence);
    }
}

public sealed class InventoryOperationIntent
{
    private InventoryOperationIntent(
        Guid operationId,
        Guid inventoryItemId,
        Guid productId,
        StockMovementType movementType,
        int quantityDelta,
        long expectedSourceVersion,
        string reason,
        string reference,
        string actor)
    {
        OperationId = operationId;
        InventoryItemId = inventoryItemId;
        ProductId = productId;
        MovementType = movementType;
        QuantityDelta = quantityDelta;
        ExpectedSourceVersion = expectedSourceVersion;
        Reason = reason;
        Reference = reference;
        Actor = actor;
    }

    public Guid OperationId { get; }

    public Guid InventoryItemId { get; }

    public Guid ProductId { get; }

    public StockMovementType MovementType { get; }

    public int QuantityDelta { get; }

    public long ExpectedSourceVersion { get; }

    public string Reason { get; }

    public string Reference { get; }

    public string Actor { get; }

    public bool Matches(StockMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return movement.Id == OperationId
            && movement.InventoryItemId == InventoryItemId
            && movement.ProductId == ProductId
            && movement.Type == MovementType
            && movement.QuantityDelta == QuantityDelta
            && ExpectedSourceVersion < long.MaxValue
            && movement.ResultingInventoryVersion == ExpectedSourceVersion + 1
            && movement.Reason == Reason
            && movement.Reference == Reference
            && movement.Actor == Actor;
    }

    public static InventoryOperationIntent Create(
        Guid operationId,
        Guid inventoryItemId,
        Guid productId,
        StockMovementType movementType,
        int quantityDelta,
        long expectedSourceVersion,
        string reason,
        string reference,
        string actor) =>
        new(
            operationId,
            inventoryItemId,
            productId,
            movementType,
            quantityDelta,
            expectedSourceVersion,
            Prepare(reason, nameof(reason)),
            Prepare(reference, nameof(reference)),
            Prepare(actor, nameof(actor)));

    public static InventoryOperationIntent FromEvidence(
        InventoryMutationEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return Create(
            evidence.OperationId,
            evidence.InventoryItemId,
            evidence.ProductId,
            evidence.MovementType,
            evidence.QuantityDelta,
            checked(evidence.ResultingInventoryVersion - 1),
            evidence.Reason,
            evidence.Reference,
            evidence.Actor);
    }

    private static string Prepare(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        return value.Trim();
    }
}

public sealed class InventoryMutationEvidence
{
    private InventoryMutationEvidence(InventoryItem item, StockMovement movement)
    {
        OperationId = movement.Id;
        InventoryItemId = item.Id;
        ProductId = item.ProductId;
        IntendedOnHandQuantity = item.OnHandQuantity;
        IntendedHeldQuantity = item.HeldQuantity;
        IntendedVersion = item.Version;
        IntendedUpdatedAtUtc = NormalizePostgresInstant(item.UpdatedAtUtc);
        IntendedUpdatedBy = item.UpdatedBy;
        MovementType = movement.Type;
        QuantityDelta = movement.QuantityDelta;
        ResultingOnHandQuantity = movement.ResultingOnHandQuantity;
        ResultingInventoryVersion = movement.ResultingInventoryVersion;
        Reason = movement.Reason;
        Reference = movement.Reference;
        Actor = movement.Actor;
        OccurredAtUtc = NormalizePostgresInstant(movement.OccurredAtUtc);
        ReservationId = movement.ReservationId;
    }

    public Guid OperationId { get; }

    public Guid InventoryItemId { get; }

    public Guid ProductId { get; }

    public int IntendedOnHandQuantity { get; }

    public int IntendedHeldQuantity { get; }

    public long IntendedVersion { get; }

    public DateTimeOffset IntendedUpdatedAtUtc { get; }

    public string IntendedUpdatedBy { get; }

    public StockMovementType MovementType { get; }

    public int QuantityDelta { get; }

    public int ResultingOnHandQuantity { get; }

    public long ResultingInventoryVersion { get; }

    public string Reason { get; }

    public string Reference { get; }

    public string Actor { get; }

    public DateTimeOffset OccurredAtUtc { get; }

    public Guid? ReservationId { get; }

    public static InventoryMutationEvidence Capture(
        InventoryItem item,
        StockMovement movement)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(movement);
        if (item.Id != movement.InventoryItemId || item.ProductId != movement.ProductId)
        {
            throw new ArgumentException(
                "Inventory mutation evidence must belong to the supplied Inventory item.",
                nameof(movement));
        }

        return new InventoryMutationEvidence(item, movement);
    }

    private static DateTimeOffset NormalizePostgresInstant(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new DateTimeOffset(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }
}
