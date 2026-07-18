using MediatR;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.CreateBrand;

public sealed class CreateBrandHandler(
    IBrandMutationSessionFactory sessionFactory,
    MediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateBrandCommand, Result<BrandMutationResult>>
{
    public async Task<Result<BrandMutationResult>> Handle(
        CreateBrandCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before creating a Brand.");
        var image = request.Image
            ?? throw new InvalidOperationException(
                "CreateBrand validation must require an image before the handler.");
        var brandId = Guid.NewGuid();
        BrandMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);

        var result = await mediaCoordinator.ExecuteAsync<BrandMutationResult, BrandMutationEvidence>(
            image,
            session,
            async (stagedMedia, operationCancellationToken) =>
            {
                await session.AcquireMutationLockAsync(operationCancellationToken);
                var normalizedDisplayName = CatalogNameNormalizer.Normalize(request.DisplayName);
                if (await session.DisplayNameExistsAsync(
                        normalizedDisplayName,
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<BrandMutationResult>.Failure(
                        BrandErrors.DuplicateDisplayName);
                }

                var normalizedEnglishName = CatalogNameNormalizer.Normalize(request.EnglishName);
                if (await session.EnglishNameExistsAsync(
                        normalizedEnglishName,
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<BrandMutationResult>.Failure(
                        BrandErrors.DuplicateEnglishName);
                }

                var slug = await session.AllocateSlugAsync(
                    request.EnglishName,
                    excludedId: null,
                    operationCancellationToken);
                var persistedDisplayName = request.DisplayName.Trim();
                var media = CatalogMediaReference.Create(
                    stagedMedia.StorageKey,
                    stagedMedia.PublicRelativeUrl,
                    $"รูปแบรนด์ {persistedDisplayName}");
                var brand = Brand.CreateWithImage(
                    brandId,
                    request.DisplayName,
                    request.EnglishName,
                    slug,
                    media,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    actor);
                session.Add(brand);
                intendedEvidence = BrandMutationEvidence.Capture(brand);
                return Result<BrandMutationResult>.Success(BrandMutationResult.From(brand));
            },
            verifyCommit: verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "CreateBrand commit verification requires intended evidence."),
                verificationCancellationToken),
            refreshResult: BrandMutationResult.From,
            new MediaMutationContext("Brand", brandId, previousMedia: null),
            cancellationToken);

        return MapImageFailure(result);
    }

    private static Result<BrandMutationResult> MapImageFailure(
        Result<BrandMutationResult> result)
    {
        if (result.IsSuccess
            || result.Error.Type != ErrorType.Validation
            || result.ValidationFailures.Count != 0)
        {
            return result;
        }

        return Result<BrandMutationResult>.Failure(
            result.Error,
            [new FieldValidationFailure(nameof(CreateBrandCommand.Image), result.Error.Message)]);
    }
}
