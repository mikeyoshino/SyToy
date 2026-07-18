using MediatR;
using ToyStore.Application.Characters;
using ToyStore.Application.Characters.CreateCharacter;
using ToyStore.Application.Characters.SearchCharacters;
using ToyStore.Application.Common.Models;
using ToyStore.Web.Components.Forms;

namespace ToyStore.Web.Components.Admin.Primitives;

public sealed class AdminCharacterAutocompleteAdapter
{
    private const string SelectUniverseMessage = "เลือกจักรวาลก่อน";
    private readonly Func<
        SearchCharactersQuery,
        CancellationToken,
        Task<Result<SearchCharactersResult>>> search;
    private readonly Func<
        CreateCharacterCommand,
        CancellationToken,
        Task<Result<CharacterOption>>> create;
    private readonly AdminRequestExecutor requestExecutor;

    public AdminCharacterAutocompleteAdapter(
        ISender sender,
        AdminRequestExecutor requestExecutor)
        : this(
            (query, cancellationToken) => sender.Send(query, cancellationToken),
            (command, cancellationToken) => sender.Send(command, cancellationToken),
            requestExecutor)
    {
        ArgumentNullException.ThrowIfNull(sender);
    }

    internal AdminCharacterAutocompleteAdapter(
        Func<SearchCharactersQuery, CancellationToken, Task<Result<SearchCharactersResult>>> search,
        Func<CreateCharacterCommand, CancellationToken, Task<Result<CharacterOption>>> create,
        AdminRequestExecutor requestExecutor)
    {
        ArgumentNullException.ThrowIfNull(search);
        ArgumentNullException.ThrowIfNull(create);
        ArgumentNullException.ThrowIfNull(requestExecutor);

        this.search = search;
        this.create = create;
        this.requestExecutor = requestExecutor;
    }

    public async Task<AutocompleteSearchResult<CharacterOption>> SearchAsync(
        Guid universeId,
        string term,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(term);
        if (universeId == Guid.Empty)
        {
            return AutocompleteSearchResults.Failed<CharacterOption>(SelectUniverseFailure());
        }

        var result = await requestExecutor.ExecuteAsync(
            operationCancellationToken => search(
                new SearchCharactersQuery(universeId, term),
                operationCancellationToken),
            cancellationToken);
        return result.IsSuccess
            ? AutocompleteSearchResults.Success(
                result.Value.Items,
                offerInlineCreate: !string.IsNullOrWhiteSpace(term)
                    && !result.Value.HasExactMatch)
            : AutocompleteSearchResults.Failed<CharacterOption>(MapFailure(result));
    }

    public async Task<AutocompleteCreateResult<CharacterOption>> CreateAsync(
        Guid universeId,
        string name,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (universeId == Guid.Empty)
        {
            return AutocompleteCreateResults.Failed<CharacterOption>(SelectUniverseFailure());
        }

        var result = await requestExecutor.ExecuteAsync(
            operationCancellationToken => create(
                new CreateCharacterCommand(universeId, name),
                operationCancellationToken),
            cancellationToken);
        return result.IsSuccess
            ? AutocompleteCreateResults.Success(result.Value)
            : AutocompleteCreateResults.Failed<CharacterOption>(MapFailure(result));
    }

    internal static AutocompleteFailure MapFailure(Result result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.IsSuccess)
        {
            throw new ArgumentException("A successful result has no autocomplete failure.", nameof(result));
        }

        var fieldFailure = result.ValidationFailures.FirstOrDefault(failure =>
            failure.PropertyName is nameof(CreateCharacterCommand.Name)
                or nameof(SearchCharactersQuery.Term));
        fieldFailure ??= result.ValidationFailures.Count > 0
            ? result.ValidationFailures[0]
            : null;
        var message = fieldFailure?.ErrorMessage ?? result.Error.Message;
        var kind = result.ValidationFailures.Count > 0
            || result.Error.Type == ErrorType.Validation
                ? AutocompleteFailureKind.Validation
                : result.Error.Type == ErrorType.Failure
                    ? AutocompleteFailureKind.System
                    : AutocompleteFailureKind.Business;
        return new AutocompleteFailure(kind, message);
    }

    private static AutocompleteFailure SelectUniverseFailure() =>
        new(AutocompleteFailureKind.Validation, SelectUniverseMessage);
}
