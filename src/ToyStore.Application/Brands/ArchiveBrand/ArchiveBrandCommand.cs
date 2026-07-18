using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Brands.ArchiveBrand;

public sealed record ArchiveBrandCommand(Guid Id, long ExpectedVersion)
    : AuthorizedBrandMutationRequest<Result<BrandMutationResult>>
{
    public override Result<BrandMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<BrandMutationResult>.Failure(requestError, validationFailures);
}
