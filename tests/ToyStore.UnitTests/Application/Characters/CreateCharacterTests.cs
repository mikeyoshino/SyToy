using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Behaviors;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.UnitTests.Application.Characters;

public sealed class CreateCharacterTests
{
    [Fact]
    public async Task ValidatorRequiresUniverseAndNameWithPreparedBoundaries()
    {
        var validator = new CreateCharacterValidator();

        var missing = await validator.ValidateAsync(
            new CreateCharacterCommand(Guid.Empty, " "),
            TestContext.Current.CancellationToken);
        var tooLong = await validator.ValidateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), new string('Ａ', 201)),
            TestContext.Current.CancellationToken);

        Assert.Contains(missing.Errors, failure =>
            failure.PropertyName == nameof(CreateCharacterCommand.UniverseId)
            && failure.ErrorMessage == "กรุณาเลือกจักรวาล");
        Assert.Contains(missing.Errors, failure =>
            failure.PropertyName == nameof(CreateCharacterCommand.Name)
            && failure.ErrorMessage == "กรุณากรอกชื่อตัวละคร");
        Assert.Contains(tooLong.Errors, failure =>
            failure.PropertyName == nameof(CreateCharacterCommand.Name)
            && failure.ErrorMessage == "ชื่อตัวละครต้องไม่เกิน 200 ตัวอักษรหลังปรับรูปแบบ");
        Assert.True((await validator.ValidateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), new string('Ａ', 200)),
            TestContext.Current.CancellationToken)).IsValid);
    }

    [Fact]
    public async Task AnonymousCreateStopsBeforeOpeningSession()
    {
        var harness = new Harness();
        var command = new CreateCharacterCommand(Guid.NewGuid(), "Batman");
        var behavior = new AuthorizationBehavior<
            CreateCharacterCommand,
            Result<CharacterOption>>(new StubAuthorization(false));

        var result = await behavior.Handle(
            command,
            cancellationToken => harness.Handler.Handle(command, cancellationToken),
            TestContext.Current.CancellationToken);

        Assert.Equal("Authorization.Unauthorized", result.Error.Code);
        Assert.Equal(0, harness.Factory.OpenCount);
    }

    [Fact]
    public async Task CreateLocksUniverseBeforeDuplicateCheckAndPersistsPreparedCharacter()
    {
        var harness = new Harness();
        var universeId = Guid.NewGuid();

        var result = await harness.CreateAsync(
            new CreateCharacterCommand(universeId, "  Ｂａｔｍａｎ  "));

        Assert.True(result.IsSuccess);
        Assert.Equal(universeId, result.Value.UniverseId);
        Assert.Equal("Ｂａｔｍａｎ", result.Value.Name);
        Assert.Equal(["lock", "name-check", "add"], harness.Session.Events);
        var character = Assert.IsType<Character>(harness.Session.AddedCharacter);
        Assert.Equal("BATMAN", character.NormalizedName);
        Assert.Equal(result.Value.Id, character.Id);
        Assert.Equal(1, harness.Session.ExecutionCount);
        Assert.Equal(1, harness.Factory.OpenCount);
    }

    [Fact]
    public async Task MissingOrArchivedUniverseReturnsTypedFailureWithoutNameReadOrInsert()
    {
        var harness = new Harness { Session = { UniverseAvailable = false } };

        var result = await harness.CreateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), "Batman"));

        Assert.Equal(CharacterErrors.UniverseUnavailable, result.Error);
        Assert.Equal(["lock"], harness.Session.Events);
        Assert.Null(harness.Session.AddedCharacter);
    }

    [Fact]
    public async Task EquivalentNameReturnsTypedDuplicateWithoutInsert()
    {
        var harness = new Harness { Session = { NameExists = true } };

        var result = await harness.CreateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), " Ｂａｔｍａｎ "));

        Assert.Equal(CharacterErrors.DuplicateName, result.Error);
        Assert.Equal(["lock", "name-check"], harness.Session.Events);
        Assert.Null(harness.Session.AddedCharacter);
    }

    [Fact]
    public async Task IndeterminateCommitVerifiesExactEvidenceWithoutRetryingCallback()
    {
        var harness = new Harness();
        harness.Session.CommitOutcome = CatalogCommitOutcome.Indeterminate;
        harness.Factory.VerificationOutcome = CatalogCommitVerification.Committed;

        var result = await harness.CreateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), "Batman"));

        Assert.True(result.IsSuccess);
        Assert.Equal(1, harness.Session.ExecutionCount);
        Assert.Equal(1, harness.Factory.VerifyCount);
        Assert.Equal(harness.Session.AddedCharacter!.Id, harness.Factory.VerifiedEvidence!.Id);
    }

    [Fact]
    public async Task IndeterminateUnverifiedCommitReturnsSafeUnknownAndNeverSuperseded()
    {
        var harness = new Harness();
        harness.Session.CommitOutcome = CatalogCommitOutcome.Indeterminate;
        harness.Factory.VerificationOutcome = CatalogCommitVerification.NotCommitted;

        var result = await harness.CreateAsync(
            new CreateCharacterCommand(Guid.NewGuid(), "Batman"));

        Assert.Equal(PersistenceErrors.CommitOutcomeUnknown, result.Error);
        Assert.Equal(1, harness.Session.ExecutionCount);
        Assert.Equal(1, harness.Factory.VerifyCount);
    }

    [Theory]
    [MemberData(nameof(ExpectedFailures))]
    public async Task ExpectedCreateFailureNeverProducesErrorLog(Error expected)
    {
        var logger = new CountingLogger<CreateCharacterCommand>();
        var behavior = new LoggingBehavior<
            CreateCharacterCommand,
            Result<CharacterOption>>(logger);

        var result = await behavior.Handle(
            new CreateCharacterCommand(Guid.NewGuid(), "Batman"),
            _ => Task.FromResult(Result<CharacterOption>.Failure(expected)),
            TestContext.Current.CancellationToken);

        Assert.Equal(expected, result.Error);
        Assert.Equal(0, logger.ErrorCount);
    }

    [Fact]
    public async Task UnexpectedCreateFaultIsErrorLoggedExactlyOnce()
    {
        var logger = new CountingLogger<CreateCharacterCommand>();
        var behavior = new LoggingBehavior<
            CreateCharacterCommand,
            Result<CharacterOption>>(logger);
        var exception = new IOException("database unavailable");

        var thrown = await Assert.ThrowsAsync<IOException>(() => behavior.Handle(
            new CreateCharacterCommand(Guid.NewGuid(), "Batman"),
            _ => Task.FromException<Result<CharacterOption>>(exception),
            TestContext.Current.CancellationToken));

        Assert.Same(exception, thrown);
        Assert.Equal(1, logger.ErrorCount);
    }

    public static TheoryData<Error> ExpectedFailures => new()
    {
        CharacterErrors.DuplicateName,
        CharacterErrors.UniverseUnavailable,
        PersistenceErrors.CommitOutcomeUnknown,
    };

    private sealed class Harness
    {
        public Harness()
        {
            Session = new FakeSession();
            Factory = new FakeFactory(Session);
            Handler = new CreateCharacterHandler(
                Factory,
                new CatalogCommitOutcomeResolver(
                    NullLogger<CatalogCommitOutcomeResolver>.Instance));
        }

        public FakeSession Session { get; }

        public FakeFactory Factory { get; }

        public CreateCharacterHandler Handler { get; }

        public Task<Result<CharacterOption>> CreateAsync(CreateCharacterCommand command)
        {
            var behavior = new AuthorizationBehavior<
                CreateCharacterCommand,
                Result<CharacterOption>>(new StubAuthorization(true));
            return behavior.Handle(
                command,
                cancellationToken => Handler.Handle(command, cancellationToken),
                TestContext.Current.CancellationToken);
        }
    }

    private sealed class FakeFactory(FakeSession session) : ICharacterMutationSessionFactory
    {
        public int OpenCount { get; private set; }

        public int VerifyCount { get; private set; }

        public CharacterMutationEvidence? VerifiedEvidence { get; private set; }

        public CatalogCommitVerification VerificationOutcome { get; set; } =
            CatalogCommitVerification.Committed;

        public ValueTask<ICharacterMutationSession> OpenAsync(
            CancellationToken cancellationToken)
        {
            OpenCount++;
            return ValueTask.FromResult<ICharacterMutationSession>(session);
        }

        public Task<CatalogCommitVerification<CharacterMutationEvidence>> VerifyCommitAsync(
            CharacterMutationEvidence evidence,
            CancellationToken cancellationToken)
        {
            VerifyCount++;
            VerifiedEvidence = evidence;
            return Task.FromResult(VerificationOutcome switch
            {
                CatalogCommitVerification.Committed =>
                    CatalogCommitVerificationResult.Committed(evidence),
                CatalogCommitVerification.NotCommitted =>
                    CatalogCommitVerificationResult.NotCommitted<CharacterMutationEvidence>(),
                CatalogCommitVerification.Unavailable =>
                    CatalogCommitVerificationResult.Unavailable<CharacterMutationEvidence>(),
                CatalogCommitVerification.Inconsistent =>
                    CatalogCommitVerificationResult.Inconsistent<CharacterMutationEvidence>(),
                _ => throw new InvalidOperationException(
                    "Character verification must not invent a Superseded outcome."),
            });
        }
    }

    private sealed class FakeSession : ICharacterMutationSession
    {
        public List<string> Events { get; } = [];

        public int ExecutionCount { get; private set; }

        public bool UniverseAvailable { get; set; } = true;

        public bool NameExists { get; set; }

        public Character? AddedCharacter { get; private set; }

        public CatalogCommitOutcome CommitOutcome { get; set; } = CatalogCommitOutcome.Committed;

        public Task<bool> LockActiveUniverseAsync(
            Guid universeId,
            CancellationToken cancellationToken)
        {
            Events.Add("lock");
            return Task.FromResult(UniverseAvailable);
        }

        public Task<bool> NameExistsAsync(
            Guid universeId,
            string normalizedName,
            CancellationToken cancellationToken)
        {
            Events.Add("name-check");
            return Task.FromResult(NameExists);
        }

        public void Add(Character character)
        {
            Events.Add("add");
            AddedCharacter = character;
        }

        public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
            Func<CancellationToken, Task<Result<T>>> operation,
            CancellationToken cancellationToken)
        {
            ExecutionCount++;
            var result = await operation(cancellationToken);
            return CommitOutcome == CatalogCommitOutcome.Indeterminate
                ? new CatalogMutationExecution<T>(
                    result,
                    CommitOutcome,
                    CatalogCommitFailure.Create(new IOException("commit acknowledgement lost")))
                : new CatalogMutationExecution<T>(
                    result,
                    result.IsSuccess ? CommitOutcome : CatalogCommitOutcome.DefinitelyRolledBack);
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class StubAuthorization(bool isAuthorized) : ICurrentUserAuthorization
    {
        public Task<CurrentUserAuthorizationResult> AuthorizeAsync(
            string policyName,
            CancellationToken cancellationToken) =>
            Task.FromResult(isAuthorized
                ? new CurrentUserAuthorizationResult(true, true, "admin-1")
                : new CurrentUserAuthorizationResult(false, false, null));
    }

    private sealed class CountingLogger<T> : ILogger<T>
    {
        public int ErrorCount { get; private set; }

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
            if (logLevel == LogLevel.Error)
            {
                ErrorCount++;
            }
        }
    }
}
