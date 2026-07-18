using ToyStore.Application.Common.Authorization;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Universes.ListUniverses;

public sealed record ListUniversesQuery(
    string? Search = null,
    CatalogReferenceListStatus Status = CatalogReferenceListStatus.Active,
    int Page = 1,
    int PageSize = 20)
    : AuthorizedResultRequest<Result<PagedResult<UniverseListItem>>>
{
    public override string RequiredPolicy => PolicyNames.CanManageProducts;

    public override Result<PagedResult<UniverseListItem>> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<PagedResult<UniverseListItem>>.Failure(requestError, validationFailures);
}
