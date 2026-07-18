using Microsoft.Extensions.Logging;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Characters.SearchCharacters;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Web.Components.Admin.Primitives;
using ToyStore.Web.Components.Forms;

namespace ToyStore.UnitTests.Web.Admin;

public sealed class AdminCharacterAutocompleteAdapterTests
{
    private static readonly Guid UniverseId =
        Guid.Parse("94000000-0000-0000-0000-000000000001");

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task SearchMapsAuthoritativeExactMetadataWithoutWebNormalization(
        bool hasExactMatch,
        bool expectedOfferInlineCreate)
    {
        SearchCharactersQuery? captured = null;
        var option = new CharacterOption(Guid.NewGuid(), UniverseId, "Spider Man");
        using var cancellationSource = new CancellationTokenSource();
        var adapter = Adapter(
            (query, cancellationToken) =>
            {
                captured = query;
                Assert.Equal(cancellationSource.Token, cancellationToken);
                return Task.FromResult(Result<SearchCharactersResult>.Success(
                    new SearchCharactersResult([option], hasExactMatch)));
            });

        var result = await adapter.SearchAsync(
            UniverseId,
            "  Ｓｐｉｄｅｒ Man  ",
            cancellationSource.Token);

        Assert.Null(result.Failure);
        Assert.Equal(expectedOfferInlineCreate, result.OfferInlineCreate);
        Assert.Equal(option, Assert.Single(result.Items));
        Assert.NotNull(captured);
        Assert.Equal(UniverseId, captured.UniverseId);
        Assert.Equal("  Ｓｐｉｄｅｒ Man  ", captured.Term);
        Assert.Equal(20, captured.Limit);
    }

    [Fact]
    public async Task BlankSearchNeverOffersInlineCreateEvenWhenApplicationHasNoExactMatch()
    {
        var adapter = Adapter(search: (_, _) => Task.FromResult(
            Result<SearchCharactersResult>.Success(
                new SearchCharactersResult([], hasExactMatch: false))));

        var result = await adapter.SearchAsync(
            UniverseId,
            " \t\r\n",
            TestContext.Current.CancellationToken);

        Assert.Null(result.Failure);
        Assert.False(result.OfferInlineCreate);
    }

    [Fact]
    public async Task CreateMapsAuthoritativeOptionAndPassesCancellation()
    {
        CreateCharacterCommand? captured = null;
        var option = new CharacterOption(Guid.NewGuid(), UniverseId, "Batman");
        using var cancellationSource = new CancellationTokenSource();
        var adapter = Adapter(
            create: (command, cancellationToken) =>
            {
                captured = command;
                Assert.Equal(cancellationSource.Token, cancellationToken);
                return Task.FromResult(Result<CharacterOption>.Success(option));
            });

        var result = await adapter.CreateAsync(
            UniverseId,
            "Batman",
            cancellationSource.Token);

        Assert.True(result.IsSuccess);
        Assert.Equal(option, result.Option);
        Assert.NotNull(captured);
        Assert.Equal(UniverseId, captured.UniverseId);
        Assert.Equal("Batman", captured.Name);
    }

    [Fact]
    public async Task EmptyUniverseFailsSafelyWithoutSendingQueryOrCommand()
    {
        var searchCalls = 0;
        var createCalls = 0;
        var adapter = Adapter(
            search: (_, _) =>
            {
                searchCalls++;
                throw new InvalidOperationException("Empty Universe must not search.");
            },
            create: (_, _) =>
            {
                createCalls++;
                throw new InvalidOperationException("Empty Universe must not create.");
            });

        var search = await adapter.SearchAsync(
            Guid.Empty,
            "Batman",
            TestContext.Current.CancellationToken);
        var create = await adapter.CreateAsync(
            Guid.Empty,
            "Batman",
            TestContext.Current.CancellationToken);

        Assert.Equal(0, searchCalls);
        Assert.Equal(0, createCalls);
        Assert.Equal(AutocompleteFailureKind.Validation, search.Failure!.Kind);
        Assert.Equal("เลือกจักรวาลก่อน", search.Failure.Message);
        Assert.Equal(AutocompleteFailureKind.Validation, create.Failure!.Kind);
        Assert.Equal("เลือกจักรวาลก่อน", create.Failure.Message);
    }

    [Fact]
    public async Task StructuredFailuresMapToThaiValidationBusinessAndSystemFeedback()
    {
        var validationAdapter = Adapter(search: (_, _) => Task.FromResult(
            Result<SearchCharactersResult>.Failure(
                RequestErrors.ValidationFailed,
                [new FieldValidationFailure(
                    nameof(SearchCharactersQuery.Term),
                    "คำค้นหาไม่ถูกต้อง")])));
        var duplicateAdapter = Adapter(create: (_, _) => Task.FromResult(
            Result<CharacterOption>.Failure(CharacterErrors.DuplicateName)));
        var unavailableAdapter = Adapter(search: (_, _) => Task.FromResult(
            Result<SearchCharactersResult>.Failure(CharacterErrors.UniverseUnavailable)));
        var commitUnknownAdapter = Adapter(create: (_, _) => Task.FromResult(
            Result<CharacterOption>.Failure(PersistenceErrors.CommitOutcomeUnknown)));

        var validation = await validationAdapter.SearchAsync(
            UniverseId,
            "term",
            TestContext.Current.CancellationToken);
        var duplicate = await duplicateAdapter.CreateAsync(
            UniverseId,
            "Batman",
            TestContext.Current.CancellationToken);
        var unavailable = await unavailableAdapter.SearchAsync(
            UniverseId,
            "Batman",
            TestContext.Current.CancellationToken);
        var commitUnknown = await commitUnknownAdapter.CreateAsync(
            UniverseId,
            "Batman",
            TestContext.Current.CancellationToken);

        Assert.Equal(
            new AutocompleteFailure(AutocompleteFailureKind.Validation, "คำค้นหาไม่ถูกต้อง"),
            validation.Failure);
        Assert.Equal(
            new AutocompleteFailure(
                AutocompleteFailureKind.Business,
                CharacterErrors.DuplicateName.Message),
            duplicate.Failure);
        Assert.Equal(
            new AutocompleteFailure(
                AutocompleteFailureKind.Business,
                CharacterErrors.UniverseUnavailable.Message),
            unavailable.Failure);
        Assert.Equal(
            new AutocompleteFailure(
                AutocompleteFailureKind.System,
                PersistenceErrors.CommitOutcomeUnknown.Message),
            commitUnknown.Failure);
    }

    [Fact]
    public async Task UnexpectedFaultUsesRequestExecutorSafeThaiSystemFailureAndLogsOnce()
    {
        var logger = new CountingLogger<AdminRequestExecutor>();
        var adapter = new AdminCharacterAutocompleteAdapter(
            (_, _) => Task.FromException<Result<SearchCharactersResult>>(
                new InvalidOperationException("sensitive provider detail")),
            (_, _) => throw new InvalidOperationException("Create is not used."),
            new AdminRequestExecutor(logger));

        var result = await adapter.SearchAsync(
            UniverseId,
            "Batman",
            TestContext.Current.CancellationToken);

        Assert.Equal(AutocompleteFailureKind.System, result.Failure!.Kind);
        Assert.Equal("เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง", result.Failure.Message);
        Assert.DoesNotContain("sensitive", result.Failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(1, logger.ErrorCount);
    }

    private static AdminCharacterAutocompleteAdapter Adapter(
        Func<SearchCharactersQuery, CancellationToken, Task<Result<SearchCharactersResult>>>? search = null,
        Func<CreateCharacterCommand, CancellationToken, Task<Result<CharacterOption>>>? create = null) =>
        new(
            search ?? ((_, _) => Task.FromResult(Result<SearchCharactersResult>.Success(
                new SearchCharactersResult([], hasExactMatch: false)))),
            create ?? ((_, _) => Task.FromResult(Result<CharacterOption>.Success(
                new CharacterOption(Guid.NewGuid(), UniverseId, "Default")))),
            new AdminRequestExecutor(new CountingLogger<AdminRequestExecutor>()));

    private sealed class CountingLogger<T> : ILogger<T>
    {
        public int ErrorCount { get; private set; }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

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
