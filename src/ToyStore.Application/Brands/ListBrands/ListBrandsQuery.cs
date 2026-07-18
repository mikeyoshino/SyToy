using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Brands.ListBrands;

public sealed record ListBrandsQuery(
    string? Search = null,
    CatalogReferenceListStatus Status = CatalogReferenceListStatus.Active,
    int Page = 1,
    int PageSize = 20)
    : AuthorizedResultRequest<Result<PagedResult<BrandListItem>>>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public override Result<PagedResult<BrandListItem>> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PagedResult<BrandListItem>>.Failure(requestError, validationFailures);
}
