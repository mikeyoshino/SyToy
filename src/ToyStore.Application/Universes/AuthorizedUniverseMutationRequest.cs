using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Universes;

public abstract record AuthorizedUniverseMutationRequest<TResponse>
    : AuthorizedResultRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            {
                Target: PersistenceFailureTarget.Universe,
                Kind: PersistenceFailureKind.DuplicateDisplayName,
            } => UniverseErrors.DuplicateDisplayName,
            {
                Target: PersistenceFailureTarget.Universe,
                Kind: PersistenceFailureKind.DuplicateEnglishName,
            } => UniverseErrors.DuplicateEnglishName,
            {
                Target: PersistenceFailureTarget.Request,
                Kind: PersistenceFailureKind.ConcurrencyConflict,
            } => UniverseErrors.StaleVersion,
            _ => null,
        };
    }
}
