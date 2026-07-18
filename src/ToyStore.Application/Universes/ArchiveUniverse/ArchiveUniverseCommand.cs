using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Universes.ArchiveUniverse;

public sealed record ArchiveUniverseCommand(Guid Id, long ExpectedVersion)
    : AuthorizedUniverseMutationRequest<Result<UniverseMutationResult>>
{
    public override Result<UniverseMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<UniverseMutationResult>.Failure(requestError, validationFailures);
}
