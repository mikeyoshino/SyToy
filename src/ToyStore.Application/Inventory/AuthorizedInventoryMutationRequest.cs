using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Inventory;

public abstract record AuthorizedInventoryMutationRequest<TResponse>
    : AuthorizedResultRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            {
                Target: PersistenceFailureTarget.Request,
                Kind: PersistenceFailureKind.ConcurrencyConflict,
            } => InventoryErrors.StaleVersion,
            _ => null,
        };
    }
}
