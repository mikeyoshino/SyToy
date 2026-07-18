using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Cart;

public abstract record AuthorizedCartRequest<TResponse>
    : AuthorizedResultRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanUseCustomerCart;

    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            {
                Target: PersistenceFailureTarget.Request,
                Kind: PersistenceFailureKind.ConcurrencyConflict,
            } => CartErrors.StaleVersion,
            {
                Target: PersistenceFailureTarget.CartOperation,
                Kind: PersistenceFailureKind.DuplicateOperation,
            } => CartErrors.OperationConflict,
            _ => null,
        };
    }
}
