using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.ArchiveProduct;

public sealed class ArchiveProductHandler(
    IProductMutationSessionFactory sessionFactory,
    CatalogCommitOutcomeResolver commitResolver,
    TimeProvider timeProvider)
    : IRequestHandler<ArchiveProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(
        ArchiveProductCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before archiving a Product.");
        ProductMutationEvidence? intendedEvidence = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var execution = await session.ExecuteOnceAsync(
            async operationCancellationToken =>
            {
                await session.AcquireNamespaceLockAsync(operationCancellationToken);
                var product = await session.LockProductAsync(
                    request.Id,
                    operationCancellationToken);
                if (product is null)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.NotFound);
                }

                if (product.SaleType != SaleType.InStock)
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.InStockLifecycleRequired);
                }

                if (product.Status != ProductStatus.Published)
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.ArchivePublishedRequired);
                }

                if (product.Version != request.ExpectedVersion)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.StaleVersion);
                }

                product.Archive(
                    request.ExpectedVersion,
                    timeProvider.GetUtcNow().ToUniversalTime(),
                    actor);
                intendedEvidence = ProductMutationEvidence.Capture(product);
                return Result<ProductMutationResult>.Success(
                    ProductMutationResult.From(product));
            },
            cancellationToken);

        return await commitResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "Archive Product verification requires intended evidence."),
                verificationCancellationToken),
            ProductMutationResult.From,
            "Product",
            cancellationToken);
    }
}
