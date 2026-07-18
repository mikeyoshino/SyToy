using Microsoft.Extensions.Logging;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Inventory;

namespace ToyStore.UnitTests.Application.Inventory;

public sealed class InventoryCommitOutcomeResolverTests
{
    [Fact]
    public async Task CommitCancellationVerifiesExactlyOnceNonCancellablyThenRethrowsOriginal()
    {
        var logger = new ListLogger();
        var resolver = new InventoryCommitOutcomeResolver(logger);
        var evidence = CreateEvidence();
        var cancellation = new OperationCanceledException("cancel during commit");
        var execution = new InventoryMutationExecution<string>(
            Result<string>.Success("intended"),
            InventoryCommitOutcome.Indeterminate,
            InventoryCommitFailure.Create(cancellation));
        var verificationCount = 0;
        var receivedToken = new CancellationToken(canceled: true);

        var thrown = await Assert.ThrowsAsync<OperationCanceledException>(() =>
            resolver.ResolveAsync(
                execution,
                cancellationToken =>
                {
                    verificationCount++;
                    receivedToken = cancellationToken;
                    return Task.FromResult(
                        InventoryCommitVerificationResult.Committed(evidence));
                },
                static authoritative => authoritative.IntendedVersion.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                "ReceiveStock",
                new CancellationToken(canceled: true)));

        Assert.Same(cancellation, thrown);
        Assert.Equal(1, verificationCount);
        Assert.False(receivedToken.CanBeCanceled);
    }

    [Fact]
    public async Task IndeterminateCommitUsesFreshSupersededEvidenceAndMapsUnsafeOutcomes()
    {
        var resolver = new InventoryCommitOutcomeResolver(new ListLogger());
        var evidence = CreateEvidence();
        var execution = new InventoryMutationExecution<string>(
            Result<string>.Success("intended"),
            InventoryCommitOutcome.Indeterminate,
            InventoryCommitFailure.Create(new InjectedCommitException()));

        var superseded = await resolver.ResolveAsync(
            execution,
            _ => Task.FromResult(
                InventoryCommitVerificationResult.Superseded(evidence)),
            static authoritative => $"version-{authoritative.IntendedVersion}",
            "ReceiveStock",
            TestContext.Current.CancellationToken);
        var inconsistent = await resolver.ResolveAsync(
            execution,
            _ => Task.FromResult(InventoryCommitVerificationResult.Inconsistent()),
            static _ => "must-not-run",
            "ReceiveStock",
            TestContext.Current.CancellationToken);

        Assert.Equal($"version-{evidence.IntendedVersion}", superseded.Value);
        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, inconsistent.Error);
    }

    [Fact]
    public async Task GlobalOperationCollisionUsesFreshEvidenceInsteadOfBubblingProviderFailure()
    {
        var logger = new ListLogger();
        var resolver = new InventoryCommitOutcomeResolver(logger);
        var evidence = CreateEvidence();
        var receivedToken = new CancellationToken(canceled: true);

        var exact = await resolver.ResolveOperationCollisionAsync(
            cancellationToken =>
            {
                receivedToken = cancellationToken;
                return Task.FromResult(
                    InventoryCommitVerificationResult.Committed(evidence));
            },
            static authoritative => $"unchanged-{authoritative.OperationId}",
            OperationConflict,
            "ReceiveStock",
            new CancellationToken(canceled: true));
        var conflict = await resolver.ResolveOperationCollisionAsync(
            _ => Task.FromResult(InventoryCommitVerificationResult.Conflict()),
            static _ => "must-not-run",
            OperationConflict,
            "ReceiveStock",
            TestContext.Current.CancellationToken);
        var inconsistent = await resolver.ResolveOperationCollisionAsync(
            _ => Task.FromResult(InventoryCommitVerificationResult.Inconsistent()),
            static _ => "must-not-run",
            OperationConflict,
            "ReceiveStock",
            TestContext.Current.CancellationToken);
        var unavailable = await resolver.ResolveOperationCollisionAsync(
            _ => Task.FromResult(InventoryCommitVerificationResult.Unavailable()),
            static _ => "must-not-run",
            OperationConflict,
            "ReceiveStock",
            TestContext.Current.CancellationToken);

        Assert.Equal($"unchanged-{evidence.OperationId}", exact.Value);
        Assert.False(receivedToken.CanBeCanceled);
        Assert.Equal(OperationConflict, conflict.Error);
        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, inconsistent.Error);
        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, unavailable.Error);
        Assert.Equal(2, logger.Levels.Count(level => level == LogLevel.Error));
    }

    private static readonly Error OperationConflict = new(
        "Inventory.OperationConflict",
        "รหัสการทำรายการนี้ถูกใช้กับข้อมูลอื่นแล้ว",
        ErrorType.Conflict);

    private static InventoryMutationEvidence CreateEvidence()
    {
        var now = new DateTimeOffset(2026, 7, 17, 0, 0, 0, TimeSpan.Zero);
        var creation = InventoryItem.Create(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1,
            "สินค้าเริ่มต้น", "initial", now, "admin");
        var movement = creation.Item.ReceiveStock(
            Guid.NewGuid(), 1, "รับสินค้า", "receive", creation.Item.Version,
            now.AddMinutes(1), "admin");
        return InventoryMutationEvidence.Capture(creation.Item, movement);
    }

    private sealed class InjectedCommitException : Exception;

    private sealed class ListLogger : ILogger<InventoryCommitOutcomeResolver>
    {
        public List<LogLevel> Levels { get; } = [];

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
        }
    }
}
