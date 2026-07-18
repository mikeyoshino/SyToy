using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Brands.UpdateBrand;

public sealed record UpdateBrandCommand(
    Guid Id,
    long ExpectedVersion,
    string DisplayName,
    string EnglishName,
    MediaUpload? ReplacementImage)
    : AuthorizedBrandMutationRequest<Result<BrandMutationResult>>
{
    public override Result<BrandMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<BrandMutationResult>.Failure(requestError, validationFailures);
}
