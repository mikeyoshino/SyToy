using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Application.Inventory.AdjustStock;
using ToyStore.Application.Inventory.ReceiveStock;
using ToyStore.Domain.Inventory;

namespace ToyStore.UnitTests.Application.Inventory;

public sealed class InventoryMutationHandlerTests
{
    [Fact]
    public async Task CommandsAreAuthorizedActorFreeAndValidatorsReturnThaiFieldFailures()
    {
        var receive = new ReceiveStockCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, 0, 0, " ", " ");
        var adjust = new AdjustStockCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, 0, 0, " ", " ");

        Assert.Equal(PolicyNames.CanManageProducts, receive.RequiredPolicy);
        Assert.Equal(PolicyNames.CanManageProducts, adjust.RequiredPolicy);
        Assert.DoesNotContain(
            receive.GetType().GetConstructors().Single().GetParameters(),
            parameter => parameter.Name!.Contains("actor", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(
            adjust.GetType().GetConstructors().Single().GetParameters(),
            parameter => parameter.Name!.Contains("actor", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(typeof(InventoryMutationResult).GetProperty("ReservableQuantity"));
        Assert.Null(typeof(InventoryMutationResult).GetProperty("AvailableQuantity"));
        Assert.Null(receive.MapPersistenceFailure(new PersistenceFailure(
            PersistenceFailureTarget.StockMovement,
            PersistenceFailureKind.DuplicateOperation)));
        var receiveFailures = await new ReceiveStockValidator().ValidateAsync(
            receive,
            TestContext.Current.CancellationToken);
        var adjustFailures = await new AdjustStockValidator().ValidateAsync(
            adjust,
            TestContext.Current.CancellationToken);

        Assert.All(receiveFailures.Errors.Concat(adjustFailures.Errors), failure =>
            Assert.Contains(
                failure.ErrorMessage,
                character => character is >= '\u0E00' and <= '\u0E7F'));
        Assert.Contains(receiveFailures.Errors, failure =>
            failure.PropertyName == nameof(ReceiveStockCommand.Quantity));
        Assert.Contains(adjustFailures.Errors, failure =>
            failure.PropertyName == nameof(AdjustStockCommand.QuantityDelta));

        var tooLongReason = new string('ก', InventoryLimits.ReasonLength + 1);
        var tooLongReference = new string('อ', InventoryLimits.ReferenceLength + 1);
        var lengths = await new ReceiveStockValidator().ValidateAsync(
            new ReceiveStockCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1,
                tooLongReason, tooLongReference),
            TestContext.Current.CancellationToken);
        Assert.Contains(lengths.Errors, failure =>
            failure.PropertyName == nameof(ReceiveStockCommand.Reason));
        Assert.Contains(lengths.Errors, failure =>
            failure.PropertyName == nameof(ReceiveStockCommand.Reference));
    }

    [Fact]
    public async Task ReceiveHappyPathUsesAuthorizedActorClockAndExactRetryBeforeStale()
    {
        var creation = CreateInventory(stock: 2);
        var harness = new Harness(creation.Item);
        var command = Receive(creation.Item, Guid.NewGuid(), 1, 2);

        var applied = await harness.ReceiveAsync(command);
        var exact = await harness.ReceiveAsync(command);

        Assert.True(applied.IsSuccess);
        Assert.True(applied.Value.Changed);
        Assert.Equal(4, applied.Value.OnHandQuantity);
        Assert.Equal("admin-1", applied.Value.UpdatedBy);
        Assert.True(exact.IsSuccess);
        Assert.False(exact.Value.Changed);
        Assert.Equal(1, harness.Clock.CallCount);
        Assert.Single(harness.Session.Movements, movement => movement.Id == command.OperationId);
    }

    [Fact]
    public async Task ExistingOperationChangedIntentFieldsAreTypedConflicts()
    {
        var creation = CreateInventory(stock: 2);
        var operationId = Guid.NewGuid();
        var harness = new Harness(creation.Item);
        Assert.True((await harness.ReceiveAsync(
            Receive(creation.Item, operationId, 1, 1))).IsSuccess);

        var commands = new[]
        {
            Receive(creation.Item, operationId, 1, 1) with { Reason = "เหตุผลอื่น" },
            Receive(creation.Item, operationId, 1, 1) with { Reference = "other" },
            Receive(creation.Item, operationId, 1, 1) with { ExpectedVersion = 2 },
        };
        foreach (var command in commands)
        {
            var result = await harness.ReceiveAsync(command);
            Assert.Equal(InventoryErrors.OperationConflict, result.Error);
        }

        var otherActor = new Harness(
            creation.Item,
            authorization: new StubAuthorization("admin-2"),
            movements: harness.Session.Movements);
        Assert.Equal(
            InventoryErrors.OperationConflict,
            (await otherActor.ReceiveAsync(Receive(creation.Item, operationId, 1, 1))).Error);
    }

    [Fact]
    public async Task AdjustHappyExactAndChangedIntentFieldsAreTyped()
    {
        var creation = CreateInventory(2);
        var operationId = Guid.NewGuid();
        var harness = new Harness(creation.Item);
        var command = Adjust(creation.Item, operationId, 1, 1);

        var applied = await harness.AdjustAsync(command);
        var exact = await harness.AdjustAsync(command);
        var conflicts = new[]
        {
            await harness.AdjustAsync(command with { QuantityDelta = 2 }),
            await harness.AdjustAsync(command with { Reason = "เหตุผลอื่น" }),
            await harness.AdjustAsync(command with { Reference = "other" }),
            await harness.AdjustAsync(command with { ExpectedVersion = 2 }),
        };
        var otherActor = new Harness(
            creation.Item,
            new StubAuthorization("admin-2"),
            harness.Session.Movements);

        Assert.True(applied.Value.Changed);
        Assert.False(exact.Value.Changed);
        Assert.All(conflicts, result =>
            Assert.Equal(InventoryErrors.OperationConflict, result.Error));
        Assert.Equal(
            InventoryErrors.OperationConflict,
            (await otherActor.AdjustAsync(command)).Error);
    }

    [Fact]
    public async Task NotFoundStaleInsufficientOverflowAndVersionExhaustionAreTypedResults()
    {
        var absent = new Harness(item: null);
        Assert.Equal(
            InventoryErrors.NotFound,
            (await absent.ReceiveAsync(new ReceiveStockCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1,
                "รับสินค้า", "missing"))).Error);
        Assert.Equal(
            InventoryErrors.NotFound,
            (await absent.AdjustAsync(new AdjustStockCommand(
                Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 1, 1,
                "ปรับสต็อก", "missing"))).Error);

        var normal = CreateInventory(stock: 2);
        var stale = new Harness(normal.Item);
        Assert.Equal(
            InventoryErrors.StaleVersion,
            (await stale.ReceiveAsync(Receive(normal.Item, Guid.NewGuid(), 2, 1))).Error);
        Assert.Equal(
            InventoryErrors.StaleVersion,
            (await stale.AdjustAsync(Adjust(normal.Item, Guid.NewGuid(), 2, 1))).Error);

        var held = CreateInventory(stock: 2);
        held.Item.Reserve(
            Guid.NewGuid(), Guid.NewGuid(), 2, UtcNow, UtcNow.AddMinutes(15),
            "รอชำระ", "hold", 1, "system");
        var insufficient = new Harness(held.Item);
        Assert.Equal(
            InventoryErrors.InsufficientOnHand,
            (await insufficient.AdjustAsync(Adjust(
                held.Item, Guid.NewGuid(), held.Item.Version, -1))).Error);

        var maximum = CreateInventory(int.MaxValue);
        var overflow = new Harness(maximum.Item);
        Assert.Equal(
            InventoryErrors.QuantityOverflow,
            (await overflow.ReceiveAsync(Receive(maximum.Item, Guid.NewGuid(), 1, 1))).Error);
        Assert.Equal(
            InventoryErrors.QuantityOverflow,
            (await overflow.AdjustAsync(Adjust(maximum.Item, Guid.NewGuid(), 1, 1))).Error);

        SetVersion(normal.Item, long.MaxValue);
        var exhausted = new Harness(normal.Item);
        Assert.Equal(
            InventoryErrors.VersionExhausted,
            (await exhausted.ReceiveAsync(Receive(
                normal.Item, Guid.NewGuid(), long.MaxValue, 1))).Error);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    public async Task UnauthorizedAndForbiddenStopBeforeValidationSessionAndClock(
        bool authenticated,
        bool authorized)
    {
        var creation = CreateInventory(2);
        var harness = new Harness(
            creation.Item,
            new StubAuthorization(
                authenticated,
                authorized,
                authenticated ? "admin-1" : null));
        var command = new ReceiveStockCommand(
            Guid.Empty, Guid.Empty, Guid.Empty, 0, 0, " ", " ");
        var validator = new CountingValidator();
        var authorizationBehavior = new AuthorizationBehavior<
            ReceiveStockCommand,
            Result<InventoryMutationResult>>(harness.Authorization);
        var validationBehavior = new ValidationBehavior<
            ReceiveStockCommand,
            Result<InventoryMutationResult>>([validator]);

        var result = await authorizationBehavior.Handle(
            command,
            cancellationToken => validationBehavior.Handle(
                command,
                token => harness.ReceiveHandler.Handle(command, token),
                cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal(
            authenticated ? ErrorType.Forbidden : ErrorType.Unauthorized,
            result.Error.Type);
        Assert.Equal(0, validator.CallCount);
        Assert.Equal(0, harness.Factory.OpenCount);
        Assert.Equal(0, harness.Clock.CallCount);
    }

    [Fact]
    public async Task NamedDuplicateOperationUsesClassifierAndFreshVerification()
    {
        var creation = CreateInventory(2);
        var operationId = Guid.NewGuid();
        var exact = new Harness(creation.Item);
        exact.Session.SaveException = new InjectedPersistenceException();
        exact.Classifier.Failure = new PersistenceFailure(
            PersistenceFailureTarget.StockMovement,
            PersistenceFailureKind.DuplicateOperation);
        exact.Factory.Verification = evidence =>
            InventoryCommitVerificationResult.Committed(evidence);

        var result = await exact.ReceiveAsync(Receive(creation.Item, operationId, 1, 1));

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Changed);
        Assert.Equal(1, exact.Factory.VerifyCount);
        Assert.Equal(1, exact.Session.CallbackCount);

        var adjustCreation = CreateInventory(2);
        var adjust = new Harness(adjustCreation.Item);
        adjust.Session.SaveException = new InjectedPersistenceException();
        adjust.Classifier.Failure = new PersistenceFailure(
            PersistenceFailureTarget.StockMovement,
            PersistenceFailureKind.DuplicateOperation);
        adjust.Factory.Verification = evidence =>
            InventoryCommitVerificationResult.Committed(evidence);
        var adjusted = await adjust.AdjustAsync(Adjust(
            adjustCreation.Item,
            Guid.NewGuid(),
            1,
            1));
        Assert.True(adjusted.IsSuccess);
        Assert.False(adjusted.Value.Changed);
        Assert.Equal(1, adjust.Factory.VerifyCount);
    }

    [Fact]
    public async Task ExactReceiveRetryReconcilesLostCommitAcknowledgementWithoutDuplicateEffect()
    {
        var creation = CreateInventory(2);
        var harness = new Harness(creation.Item);
        var command = Receive(creation.Item, Guid.NewGuid(), 1, 1);
        Assert.True((await harness.ReceiveAsync(command)).IsSuccess);
        harness.Session.ForceIndeterminate = true;

        var retry = await harness.ReceiveAsync(command);

        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value.Changed);
        Assert.Equal(1, harness.Factory.VerifyCount);
        Assert.Single(harness.Session.Movements, movement => movement.Id == command.OperationId);
    }

    [Fact]
    public async Task ExactAdjustRetryReconcilesLostCommitAcknowledgementWithoutDuplicateEffect()
    {
        var creation = CreateInventory(2);
        var harness = new Harness(creation.Item);
        var command = Adjust(creation.Item, Guid.NewGuid(), 1, 1);
        Assert.True((await harness.AdjustAsync(command)).IsSuccess);
        harness.Session.ForceIndeterminate = true;

        var retry = await harness.AdjustAsync(command);

        Assert.True(retry.IsSuccess);
        Assert.False(retry.Value.Changed);
        Assert.Equal(1, harness.Factory.VerifyCount);
        Assert.Single(harness.Session.Movements, movement => movement.Id == command.OperationId);
    }

    [Fact]
    public async Task TypedBusinessFailureDoesNotProduceErrorLog()
    {
        var creation = CreateInventory(2);
        var harness = new Harness(creation.Item);
        var command = Receive(creation.Item, Guid.NewGuid(), 2, 1);
        var logger = new ListLogger<ReceiveStockCommand>();
        var logging = new LoggingBehavior<
            ReceiveStockCommand,
            Result<InventoryMutationResult>>(logger);

        var result = await logging.Handle(
            command,
            token => harness.ReceiveAsync(command),
            TestContext.Current.CancellationToken);

        Assert.Equal(InventoryErrors.StaleVersion, result.Error);
        Assert.DoesNotContain(LogLevel.Error, logger.Levels);
    }

    [Theory]
    [InlineData(true, "lower-version")]
    [InlineData(true, "on-hand")]
    [InlineData(true, "updated-at")]
    [InlineData(true, "updated-by")]
    [InlineData(false, "lower-version")]
    [InlineData(false, "on-hand")]
    [InlineData(false, "updated-at")]
    [InlineData(false, "updated-by")]
    public async Task ExactRetryRejectsCorruptedOwningInventoryState(
        bool receive,
        string corruption)
    {
        var scenario = CreateAppliedScenario(receive);
        Corrupt(scenario.Item, corruption);
        var harness = new Harness(scenario.Item, movements: [scenario.Movement]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => receive
            ? harness.ReceiveAsync((ReceiveStockCommand)scenario.Command)
            : harness.AdjustAsync((AdjustStockCommand)scenario.Command));
    }

    [Fact]
    public async Task CorruptedExactRetryPropagatesSystemFailureAndLogsErrorExactlyOnce()
    {
        var scenario = CreateAppliedScenario(receive: true);
        Corrupt(scenario.Item, "updated-by");
        var harness = new Harness(scenario.Item, movements: [scenario.Movement]);
        var command = (ReceiveStockCommand)scenario.Command;
        var logger = new ListLogger<ReceiveStockCommand>();
        var logging = new LoggingBehavior<
            ReceiveStockCommand,
            Result<InventoryMutationResult>>(logger);

        await Assert.ThrowsAsync<InvalidOperationException>(() => logging.Handle(
            command,
            _ => harness.ReceiveAsync(command),
            TestContext.Current.CancellationToken));

        Assert.Equal(1, logger.Levels.Count(level => level == LogLevel.Error));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExactRetryAllowsOwningInventorySupersededByLaterMovement(bool receive)
    {
        var scenario = CreateAppliedScenario(receive);
        _ = scenario.Item.ReceiveStock(
            Guid.NewGuid(), 1, "รับสินค้าเพิ่ม", "later", scenario.Item.Version,
            UtcNow.AddMinutes(1), "admin-1");
        var harness = new Harness(scenario.Item, movements: [scenario.Movement]);

        var result = receive
            ? await harness.ReceiveAsync((ReceiveStockCommand)scenario.Command)
            : await harness.AdjustAsync((AdjustStockCommand)scenario.Command);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Changed);
        Assert.Equal(3, result.Value.Version);
    }

    private static readonly DateTimeOffset UtcNow =
        new(2026, 7, 17, 2, 0, 0, TimeSpan.Zero);

    private static InventoryCreation CreateInventory(int stock) => InventoryItem.Create(
        Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), stock,
        "สินค้าเริ่มต้น", "initial", UtcNow.AddMinutes(-1), "creator");

    private static ReceiveStockCommand Receive(
        InventoryItem item,
        Guid operationId,
        long version,
        int quantity) => new(
            item.Id, item.ProductId, operationId, version, quantity,
            "รับสินค้า", "receive");

    private static AdjustStockCommand Adjust(
        InventoryItem item,
        Guid operationId,
        long version,
        int delta) => new(
            item.Id, item.ProductId, operationId, version, delta,
            "ปรับสต็อก", "adjust");

    private static void SetVersion(InventoryItem item, long version) =>
        typeof(InventoryItem).GetProperty(nameof(InventoryItem.Version))!
            .SetValue(item, version);

    private static AppliedScenario CreateAppliedScenario(bool receive)
    {
        var creation = CreateInventory(2);
        var operationId = Guid.NewGuid();
        if (receive)
        {
            var command = Receive(creation.Item, operationId, 1, 1);
            var movement = creation.Item.ReceiveStock(
                operationId, 1, command.Reason, command.Reference, 1,
                UtcNow.AddTicks(7), "admin-1");
            return new AppliedScenario(creation.Item, movement, command);
        }

        var adjust = Adjust(creation.Item, operationId, 1, 1);
        var adjustedMovement = creation.Item.AdjustStock(
            operationId, 1, adjust.Reason, adjust.Reference, 1,
            UtcNow.AddTicks(7), "admin-1");
        return new AppliedScenario(creation.Item, adjustedMovement, adjust);
    }

    private static void Corrupt(InventoryItem item, string corruption)
    {
        var property = corruption switch
        {
            "lower-version" => nameof(InventoryItem.Version),
            "on-hand" => nameof(InventoryItem.OnHandQuantity),
            "updated-at" => nameof(InventoryItem.UpdatedAtUtc),
            "updated-by" => nameof(InventoryItem.UpdatedBy),
            _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
        };
        object value = corruption switch
        {
            "lower-version" => 1L,
            "on-hand" => item.OnHandQuantity + 1,
            "updated-at" => item.UpdatedAtUtc.AddMinutes(1),
            "updated-by" => "corrupted-actor",
            _ => throw new ArgumentOutOfRangeException(nameof(corruption)),
        };
        typeof(InventoryItem).GetProperty(property)!.SetValue(item, value);
    }

    private sealed record AppliedScenario(
        InventoryItem Item,
        StockMovement Movement,
        object Command);

    private sealed class Harness
    {
        public Harness(
            InventoryItem? item,
            ICurrentUserAuthorization? authorization = null,
            IEnumerable<StockMovement>? movements = null)
        {
            Session = new FakeSession(item, movements);
            Factory = new FakeFactory(Session);
            Authorization = authorization ?? new StubAuthorization("admin-1");
            ReceiveHandler = new ReceiveStockHandler(
                Factory,
                new InventoryCommitOutcomeResolver(
                    NullLogger<InventoryCommitOutcomeResolver>.Instance),
                Classifier,
                Clock);
            AdjustHandler = new AdjustStockHandler(
                Factory,
                new InventoryCommitOutcomeResolver(
                    NullLogger<InventoryCommitOutcomeResolver>.Instance),
                Classifier,
                Clock);
        }

        public FakeSession Session { get; }

        public FakeFactory Factory { get; }

        public FakeClassifier Classifier { get; } = new();

        public CountingTimeProvider Clock { get; } = new();

        public ICurrentUserAuthorization Authorization { get; }

        public ReceiveStockHandler ReceiveHandler { get; }

        public AdjustStockHandler AdjustHandler { get; }

        public Task<Result<InventoryMutationResult>> ReceiveAsync(ReceiveStockCommand command) =>
            new AuthorizationBehavior<ReceiveStockCommand, Result<InventoryMutationResult>>(
                Authorization).Handle(
                    command,
                    token => ReceiveHandler.Handle(command, token),
                    TestContext.Current.CancellationToken);

        public Task<Result<InventoryMutationResult>> AdjustAsync(AdjustStockCommand command) =>
            new AuthorizationBehavior<AdjustStockCommand, Result<InventoryMutationResult>>(
                Authorization).Handle(
                    command,
                    token => AdjustHandler.Handle(command, token),
                    TestContext.Current.CancellationToken);
    }

    private sealed class FakeFactory(FakeSession session) : IInventoryMutationSessionFactory
    {
        public int OpenCount { get; private set; }

        public int VerifyCount { get; private set; }

        public Func<InventoryMutationEvidence, InventoryCommitVerificationResult> Verification { get; set; } =
            InventoryCommitVerificationResult.Committed;

        public ValueTask<IInventoryMutationSession> OpenAsync(CancellationToken cancellationToken)
        {
            OpenCount++;
            return ValueTask.FromResult<IInventoryMutationSession>(session);
        }

        public Task<InventoryCommitVerificationResult> VerifyCommitAsync(
            InventoryMutationEvidence evidence,
            CancellationToken cancellationToken)
        {
            VerifyCount++;
            return Task.FromResult(Verification(evidence));
        }
    }

    private sealed class FakeSession(
        InventoryItem? item,
        IEnumerable<StockMovement>? movements = null) : IInventoryMutationSession
    {
        public List<StockMovement> Movements { get; } = movements?.ToList() ?? [];

        public Exception? SaveException { get; set; }

        public bool ForceIndeterminate { get; set; }

        public int CallbackCount { get; private set; }

        public Task<InventoryItem?> LockInventoryAsync(
            Guid inventoryItemId,
            Guid productId,
            CancellationToken cancellationToken) =>
            Task.FromResult(
                item?.Id == inventoryItemId && item.ProductId == productId ? item : null);

        public Task<StockMovement?> FindMovementAsync(
            Guid operationId,
            CancellationToken cancellationToken) =>
            Task.FromResult(Movements.SingleOrDefault(movement => movement.Id == operationId));

        public Task<StockReservation?> FindReservationAsync(
            Guid reservationId,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public void Add(InventoryCreation creation) => throw new NotSupportedException();

        public void Add(StockMovement movement) => Movements.Add(movement);

        public void Add(StockReservation reservation) => throw new NotSupportedException();

        public async Task<InventoryMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            CallbackCount++;
            var result = await operation(cancellationToken);
            if (SaveException is not null && result.IsSuccess)
            {
                throw SaveException;
            }

            if (ForceIndeterminate && result.IsSuccess)
            {
                return new InventoryMutationExecution<T>(
                    result,
                    InventoryCommitOutcome.Indeterminate,
                    InventoryCommitFailure.Create(new InjectedCommitException()));
            }

            return new InventoryMutationExecution<T>(
                result,
                result.IsSuccess
                    ? InventoryCommitOutcome.Committed
                    : InventoryCommitOutcome.DefinitelyRolledBack);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeClassifier : IPersistenceFailureClassifier
    {
        public PersistenceFailure? Failure { get; set; }

        public PersistenceFailure? Classify(Exception exception) => Failure;
    }

    private sealed class CountingTimeProvider : TimeProvider
    {
        public int CallCount { get; private set; }

        public override DateTimeOffset GetUtcNow()
        {
            CallCount++;
            return UtcNow;
        }
    }

    private sealed class StubAuthorization : ICurrentUserAuthorization
    {
        private readonly CurrentUserAuthorizationResult result;

        public StubAuthorization(string actor) : this(true, true, actor)
        {
        }

        public StubAuthorization(bool authenticated, bool authorized, string? actor) =>
            result = new CurrentUserAuthorizationResult(authenticated, authorized, actor);

        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class CountingValidator : AbstractValidator<ReceiveStockCommand>
    {
        public CountingValidator()
        {
            RuleFor(command => command.InventoryItemId).Custom((_, context) =>
            {
                CallCount++;
                context.AddFailure("invalid");
            });
        }

        public int CallCount { get; private set; }
    }

    private sealed class ListLogger<T> : ILogger<T>
    {
        public List<LogLevel> Levels { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => Levels.Add(logLevel);
    }

    private sealed class InjectedPersistenceException : Exception;

    private sealed class InjectedCommitException : Exception;
}
