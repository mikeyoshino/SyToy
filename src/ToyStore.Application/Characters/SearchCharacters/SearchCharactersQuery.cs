using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Characters.SearchCharacters;

public sealed record SearchCharactersQuery(
    Guid UniverseId,
    string? Term = null,
    int Limit = 20)
    : AuthorizedCharacterRequest<Result<SearchCharactersResult>>
{
    public override Result<SearchCharactersResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<SearchCharactersResult>.Failure(requestError, validationFailures);
}
