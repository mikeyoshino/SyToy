using MediatR;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.UpdateUniverse;

public sealed class UpdateUniverseHandler(
    IUniverseMutationSessionFactory sessionFactory,
    MediaMutationCoordinator mediaCoordinator,
    CatalogCommitOutcomeResolver commitOutcomeResolver,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateUniverseCommand, Result<UniverseMutationResult>>
{
    public async Task<Result<UniverseMutationResult>> Handle(
        UpdateUniverseCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before updating a Universe.");
        UniverseMutationEvidence? intendedEvidence = null;
        CatalogMediaReference? previousLogo = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);

        async Task<Result<UniverseMutationResult>> MutateAsync(
            StagedMedia? stagedMedia,
            CancellationToken operationCancellationToken)
        {
            await session.AcquireMutationLockAsync(operationCancellationToken);
            var universe = await session.FindAsync(request.Id, operationCancellationToken);
            if (universe is null)
            {
                return Result<UniverseMutationResult>.Failure(UniverseErrors.NotFound);
            }

            previousLogo = universe.Logo;
            if (universe.Status == CatalogReferenceStatus.Archived)
            {
                return Result<UniverseMutationResult>.Failure(UniverseErrors.Archived);
            }

            if (universe.Version != request.ExpectedVersion)
            {
                return Result<UniverseMutationResult>.Failure(UniverseErrors.StaleVersion);
            }

            if (await session.DisplayNameExistsAsync(
                    CatalogNameNormalizer.Normalize(request.DisplayName),
                    request.Id,
                    operationCancellationToken))
            {
                return Result<UniverseMutationResult>.Failure(
                    UniverseErrors.DuplicateDisplayName);
            }

            if (await session.EnglishNameExistsAsync(
                    CatalogNameNormalizer.Normalize(request.EnglishName),
                    request.Id,
                    operationCancellationToken))
            {
                return Result<UniverseMutationResult>.Failure(
                    UniverseErrors.DuplicateEnglishName);
            }

            var persistedEnglishName = request.EnglishName.Trim();
            var englishNameChanged = !string.Equals(
                universe.EnglishName,
                persistedEnglishName,
                StringComparison.Ordinal);
            var slug = englishNameChanged
                ? await session.AllocateSlugAsync(
                    request.EnglishName,
                    request.Id,
                    operationCancellationToken)
                : universe.Slug;
            if (stagedMedia is null && previousLogo is null)
            {
                return Result<UniverseMutationResult>.Failure(UniverseErrors.MissingMedia);
            }

            var persistedDisplayName = request.DisplayName.Trim();
            var resultingLogo = stagedMedia is not null
                ? CatalogMediaReference.Create(
                    stagedMedia.StorageKey,
                    stagedMedia.PublicRelativeUrl,
                    $"โลโก้จักรวาล {persistedDisplayName}")
                : string.Equals(
                    universe.DisplayName,
                    persistedDisplayName,
                    StringComparison.Ordinal)
                    ? previousLogo!
                    : CatalogMediaReference.Create(
                    previousLogo!.StorageKey,
                    previousLogo.PublicRelativeUrl,
                    $"โลโก้จักรวาล {persistedDisplayName}");

            universe.UpdateDetailsWithLogo(
                request.DisplayName,
                request.EnglishName,
                slug,
                resultingLogo,
                request.ExpectedVersion,
                timeProvider.GetUtcNow().ToUniversalTime(),
                actor);
            intendedEvidence = UniverseMutationEvidence.Capture(universe);
            return Result<UniverseMutationResult>.Success(
                UniverseMutationResult.From(universe));
        }

        Result<UniverseMutationResult> result;
        if (request.ReplacementLogo is not null)
        {
            result = await mediaCoordinator.ExecuteAsync<
                UniverseMutationResult,
                UniverseMutationEvidence>(
                request.ReplacementLogo,
                session,
                (stagedMedia, operationCancellationToken) =>
                    MutateAsync(stagedMedia, operationCancellationToken),
                verifyCommit: verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                    intendedEvidence ?? throw new InvalidOperationException(
                        "UpdateUniverse commit verification requires intended evidence."),
                    verificationCancellationToken),
                refreshResult: UniverseMutationResult.From,
                new MediaMutationContext("Universe", request.Id, previousMedia: null),
                previousMediaAccessor: () => previousLogo,
                cancellationToken);
        }
        else
        {
            var execution = await session.ExecuteOnceAsync(
                operationCancellationToken => MutateAsync(null, operationCancellationToken),
                cancellationToken);
            result = await commitOutcomeResolver.ResolveAsync(
                execution,
                verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                    intendedEvidence ?? throw new InvalidOperationException(
                        "UpdateUniverse commit verification requires intended evidence."),
                    verificationCancellationToken),
                UniverseMutationResult.From,
                "Universe",
                cancellationToken);
        }

        return MapLogoFailure(result);
    }

    private static Result<UniverseMutationResult> MapLogoFailure(
        Result<UniverseMutationResult> result)
    {
        if (result.IsSuccess
            || result.Error.Type != ErrorType.Validation
            || result.ValidationFailures.Count != 0)
        {
            return result;
        }

        return Result<UniverseMutationResult>.Failure(
            result.Error,
            [new FieldValidationFailure(
                nameof(UpdateUniverseCommand.ReplacementLogo),
                result.Error.Message)]);
    }
}
