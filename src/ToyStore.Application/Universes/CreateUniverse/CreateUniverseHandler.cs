using MediatR;
using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.CreateUniverse;

public sealed class CreateUniverseHandler(
    IUniverseMutationSessionFactory sessionFactory,
    MediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateUniverseCommand, Result<UniverseMutationResult>>
{
    public async Task<Result<UniverseMutationResult>> Handle(
        CreateUniverseCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before creating a Universe.");
        var logo = request.Logo
            ?? throw new InvalidOperationException(
                "CreateUniverse validation must require a logo before the handler.");
        var universeId = Guid.NewGuid();
        UniverseMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);

        var result = await mediaCoordinator.ExecuteAsync<
            UniverseMutationResult,
            UniverseMutationEvidence>(
            logo,
            session,
            async (stagedMedia, operationCancellationToken) =>
            {
                await session.AcquireMutationLockAsync(operationCancellationToken);
                if (await session.DisplayNameExistsAsync(
                        CatalogNameNormalizer.Normalize(request.DisplayName),
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<UniverseMutationResult>.Failure(
                        UniverseErrors.DuplicateDisplayName);
                }

                if (await session.EnglishNameExistsAsync(
                        CatalogNameNormalizer.Normalize(request.EnglishName),
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<UniverseMutationResult>.Failure(
                        UniverseErrors.DuplicateEnglishName);
                }

                var slug = await session.AllocateSlugAsync(
                    request.EnglishName,
                    excludedId: null,
                    operationCancellationToken);
                var persistedDisplayName = request.DisplayName.Trim();
                var media = CatalogMediaReference.Create(
                    stagedMedia.StorageKey,
                    stagedMedia.PublicRelativeUrl,
                    $"โลโก้จักรวาล {persistedDisplayName}");
                var universe = Universe.CreateWithLogo(
                    universeId,
                    request.DisplayName,
                    request.EnglishName,
                    slug,
                    media,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    actor);
                session.Add(universe);
                intendedEvidence = UniverseMutationEvidence.Capture(universe);
                return Result<UniverseMutationResult>.Success(
                    UniverseMutationResult.From(universe));
            },
            verifyCommit: verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "CreateUniverse commit verification requires intended evidence."),
                verificationCancellationToken),
            refreshResult: UniverseMutationResult.From,
            new MediaMutationContext("Universe", universeId, previousMedia: null),
            cancellationToken);

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
            [new FieldValidationFailure(nameof(CreateUniverseCommand.Logo), result.Error.Message)]);
    }
}
