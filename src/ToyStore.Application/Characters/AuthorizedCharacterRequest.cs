using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Characters;

public abstract record AuthorizedCharacterRequest<TResponse>
    : AuthorizedResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;
}

public abstract record AuthorizedCharacterMutationRequest<TResponse>
    : AuthorizedCharacterRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure is
        {
            Target: PersistenceFailureTarget.Character,
            Kind: PersistenceFailureKind.DuplicateName,
        }
            ? CharacterErrors.DuplicateName
            : null;
    }
}
