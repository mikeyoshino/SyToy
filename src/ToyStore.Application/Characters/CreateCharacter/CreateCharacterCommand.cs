using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Characters.CreateCharacter;

public sealed record CreateCharacterCommand(Guid UniverseId, string Name)
    : AuthorizedCharacterMutationRequest<Result<CharacterOption>>
{
    public override Result<CharacterOption> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<CharacterOption>.Failure(requestError, validationFailures);
}
