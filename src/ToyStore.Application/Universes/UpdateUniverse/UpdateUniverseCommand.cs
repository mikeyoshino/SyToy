using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Universes.UpdateUniverse;

public sealed record UpdateUniverseCommand(
    Guid Id,
    long ExpectedVersion,
    string DisplayName,
    string EnglishName,
    MediaUpload? ReplacementLogo)
    : AuthorizedUniverseMutationRequest<Result<UniverseMutationResult>>
{
    public override Result<UniverseMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<UniverseMutationResult>.Failure(requestError, validationFailures);
}
