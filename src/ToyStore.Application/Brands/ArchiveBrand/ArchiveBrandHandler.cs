using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Catalog;

namespace ToyStore.Application.Brands.ArchiveBrand;

public sealed class ArchiveBrandHandler(
    IBrandMutationSessionFactory sessionFactory,
    CatalogCommitOutcomeResolver commitResolver,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveBrandCommand, Result<BrandMutationResult>>
{
    public async Task<Result<BrandMutationResult>> Handle(
        ArchiveBrandCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before archiving a Brand.");
        BrandMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async operationCancellationToken =>
            {
                var brand = await session.FindAsync(request.Id, operationCancellationToken);
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

                brand.Archive(
                    request.ExpectedVersion,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    actor);
                intendedEvidence = BrandMutationEvidence.Capture(brand);
                return Result<BrandMutationResult>.Success(BrandMutationResult.From(brand));
            },
            cancellationToken);

        return await commitResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "ArchiveBrand commit verification requires intended evidence."),
                verificationCancellationToken),
            BrandMutationResult.From,
            "Brand",
            cancellationToken);
    }
}
