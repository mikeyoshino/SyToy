using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Universes.CreateUniverse;

public sealed record CreateUniverseCommand(
    string DisplayName,
    string EnglishName,
    MediaUpload? Logo)
    : AuthorizedUniverseMutationRequest<Result<UniverseMutationResult>>
{
    public override Result<UniverseMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<UniverseMutationResult>.Failure(requestError, validationFailures);
}
