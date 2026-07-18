using MediatR;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.UpdateBrand;

public sealed class UpdateBrandHandler(
    IBrandMutationSessionFactory sessionFactory,
    MediaMutationCoordinator mediaCoordinator,
    CatalogCommitOutcomeResolver commitResolver,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateBrandCommand, Result<BrandMutationResult>>
{
    public async Task<Result<BrandMutationResult>> Handle(
        UpdateBrandCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before updating a Brand.");
        BrandMutationEvidence? intendedEvidence = null;
        CatalogMediaReference? lockedPreviousMedia = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);

        if (request.ReplacementImage is not null)
        {
            var mediaResult = await mediaCoordinator.ExecuteAsync<
                BrandMutationResult,
                BrandMutationEvidence>(
                request.ReplacementImage,
                session,
                (stagedMedia, operationCancellationToken) => MutateAsync(
                    session,
                    request,
                    stagedMedia,
                    actor,
                    evidence => intendedEvidence = evidence,
                    previous => lockedPreviousMedia = previous,
                    operationCancellationToken),
                verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                    RequireEvidence(intendedEvidence),
                    verificationCancellationToken),
                BrandMutationResult.From,
                new MediaMutationContext("Brand", request.Id, previousMedia: null),
                () => lockedPreviousMedia,
                cancellationToken);
            return MapImageFailure(mediaResult);
        }

        var execution = await session.ExecuteOnceAsync(
            operationCancellationToken => MutateAsync(
                session,
                request,
                stagedMedia: null,
                actor,
                evidence => intendedEvidence = evidence,
                previous => lockedPreviousMedia = previous,
                operationCancellationToken),
            cancellationToken);
        var result = await commitResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                RequireEvidence(intendedEvidence),
                verificationCancellationToken),
            BrandMutationResult.From,
            "Brand",
            cancellationToken);
        return MapImageFailure(result);
    }

    private async Task<Result<BrandMutationResult>> MutateAsync(
        IBrandMutationSession session,
        UpdateBrandCommand request,
        StagedMedia? stagedMedia,
        string actor,
        Action<BrandMutationEvidence> captureEvidence,
        Action<CatalogMediaReference?> capturePreviousMedia,
        CancellationToken cancellationToken)
    {
        await session.AcquireMutationLockAsync(cancellationToken);
        var brand = await session.FindAsync(request.Id, cancellationToken);
        if (brand is null)
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.NotFound);
        }

        if (brand.Status == CatalogReferenceStatus.Archived)
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.Archived);
        }

        if (brand.Version != request.ExpectedVersion)
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.StaleVersion);
        }

        if (await session.DisplayNameExistsAsync(
                CatalogNameNormalizer.Normalize(request.DisplayName),
                brand.Id,
                cancellationToken))
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.DuplicateDisplayName);
        }

        if (await session.EnglishNameExistsAsync(
                CatalogNameNormalizer.Normalize(request.EnglishName),
                brand.Id,
                cancellationToken))
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.DuplicateEnglishName);
        }

        var persistedDisplayName = request.DisplayName.Trim();
        var persistedEnglishName = request.EnglishName.Trim();
        var displayNameChanged = !string.Equals(
            brand.DisplayName,
            persistedDisplayName,
            StringComparison.Ordinal);
        var slug = string.Equals(
            brand.EnglishName,
            persistedEnglishName,
            StringComparison.Ordinal)
                ? brand.Slug
                : await session.AllocateSlugAsync(
                    request.EnglishName,
                    brand.Id,
                    cancellationToken);
        capturePreviousMedia(brand.Image);
        CatalogMediaReference media;
        if (stagedMedia is not null)
        {
            media = CatalogMediaReference.Create(
                stagedMedia.StorageKey,
                stagedMedia.PublicRelativeUrl,
                $"รูปแบรนด์ {persistedDisplayName}");
        }
        else if (brand.Image is not null && !displayNameChanged)
        {
            media = brand.Image;
        }
        else if (brand.Image is not null)
        {
            media = CatalogMediaReference.Create(
                brand.Image.StorageKey,
                brand.Image.PublicRelativeUrl,
                $"รูปแบรนด์ {persistedDisplayName}");
        }
        else
        {
            return Result<BrandMutationResult>.Failure(BrandErrors.MissingMedia);
        }

        brand.UpdateDetailsWithImage(
            request.DisplayName,
            request.EnglishName,
            slug,
            media,
            request.ExpectedVersion,
            timeProvider.GetUtcNow().ToUniversalTime(),
            actor);
        var evidence = BrandMutationEvidence.Capture(brand);
        captureEvidence(evidence);
        return Result<BrandMutationResult>.Success(BrandMutationResult.From(brand));
    }

    private static BrandMutationEvidence RequireEvidence(
        BrandMutationEvidence? evidence) =>
        evidence ?? throw new InvalidOperationException(
            "UpdateBrand commit verification requires intended evidence.");

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
            [new FieldValidationFailure(
                nameof(UpdateBrandCommand.ReplacementImage),
                result.Error.Message)]);
    }
}
