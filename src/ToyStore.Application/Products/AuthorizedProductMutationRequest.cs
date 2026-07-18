using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Products;

public abstract record AuthorizedProductMutationRequest<TResponse>
    : AuthorizedResultRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            {
                Target: PersistenceFailureTarget.Product,
                Kind: PersistenceFailureKind.DuplicateDisplayName,
            } => ProductErrors.DuplicateDisplayName,
            {
                Target: PersistenceFailureTarget.Product,
                Kind: PersistenceFailureKind.DuplicateEnglishName,
            } => ProductErrors.DuplicateEnglishName,
            {
                Target: PersistenceFailureTarget.Request,
                Kind: PersistenceFailureKind.ConcurrencyConflict,
            } => ProductErrors.StaleVersion,
            _ => null,
        };
    }
}
