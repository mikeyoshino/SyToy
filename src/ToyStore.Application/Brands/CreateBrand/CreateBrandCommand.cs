using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;

namespace ToyStore.Application.Brands.CreateBrand;

public sealed record CreateBrandCommand(
    string DisplayName,
    string EnglishName,
    MediaUpload? Image)
    : AuthorizedBrandMutationRequest<Result<BrandMutationResult>>
{
    public override Result<BrandMutationResult> CreateFailure(
        Error requestError,
        IReadOnlyList<FieldValidationFailure>? validationFailures = null) =>
        Result<BrandMutationResult>.Failure(requestError, validationFailures);
}
