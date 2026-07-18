using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;

namespace ToyStore.Application.Brands;

public abstract record AuthorizedBrandMutationRequest<TResponse>
    : AuthorizedResultRequest<TResponse>, IPersistenceFailureResultRequest<TResponse>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public Error? MapPersistenceFailure(PersistenceFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return failure switch
        {
            {
                Target: PersistenceFailureTarget.Brand,
                Kind: PersistenceFailureKind.DuplicateDisplayName,
            } => BrandErrors.DuplicateDisplayName,
            {
                Target: PersistenceFailureTarget.Brand,
                Kind: PersistenceFailureKind.DuplicateEnglishName,
            } => BrandErrors.DuplicateEnglishName,
            {
                Target: PersistenceFailureTarget.Request,
                Kind: PersistenceFailureKind.ConcurrencyConflict,
            } => BrandErrors.StaleVersion,
            _ => null,
        };
    }
}
