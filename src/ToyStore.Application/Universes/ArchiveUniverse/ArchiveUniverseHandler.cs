using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Universes.ArchiveUniverse;

public sealed class ArchiveUniverseHandler(
    IUniverseMutationSessionFactory sessionFactory,
    CatalogCommitOutcomeResolver commitOutcomeResolver,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveUniverseCommand, Result<UniverseMutationResult>>
{
    public async Task<Result<UniverseMutationResult>> Handle(
        ArchiveUniverseCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before archiving a Universe.");
        UniverseMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async operationCancellationToken =>
            {
                var universe = await session.FindAsync(request.Id, operationCancellationToken);
                if (universe is null)
                {
                    return Result<UniverseMutationResult>.Failure(UniverseErrors.NotFound);
                }

                if (universe.Status == CatalogReferenceStatus.Archived)
                {
                    return Result<UniverseMutationResult>.Failure(UniverseErrors.Archived);
                }

                if (universe.Version != request.ExpectedVersion)
                {
                    return Result<UniverseMutationResult>.Failure(UniverseErrors.StaleVersion);
                }

                universe.Archive(
                    request.ExpectedVersion,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    actor);
                intendedEvidence = UniverseMutationEvidence.Capture(universe);
                return Result<UniverseMutationResult>.Success(
                    UniverseMutationResult.From(universe));
            },
            cancellationToken);

        return await commitOutcomeResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "ArchiveUniverse commit verification requires intended evidence."),
                verificationCancellationToken),
            UniverseMutationResult.From,
            "Universe",
            cancellationToken);
    }
}
