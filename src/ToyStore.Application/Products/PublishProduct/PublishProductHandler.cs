using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.PublishProduct;

public sealed class PublishProductHandler(
    IProductMutationSessionFactory sessionFactory,
    CatalogCommitOutcomeResolver commitResolver,
    TimeProvider timeProvider)
    : IRequestHandler<PublishProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(
        PublishProductCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before publishing a Product.");
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

                if (product.Status != ProductStatus.Draft)
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.PublishDraftRequired);
                }

                if (product.Version != request.ExpectedVersion)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.StaleVersion);
                }

                if (product.Images.Count == 0)
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.PublishRequiresImage);
                }

                var characterIds = product.Characters
                    .Select(link => link.CharacterId)
                    .ToArray();
                var readiness = await session.LockReferencesAsync(
                    product.ProductCategoryId,
                    product.BrandId,
                    product.UniverseId,
                    characterIds,
                    operationCancellationToken);
                var readinessError = ValidatePublishReadiness(readiness, characterIds);
                if (readinessError is not null)
                {
                    return Result<ProductMutationResult>.Failure(readinessError);
                }

                var now = timeProvider.GetUtcNow().ToUniversalTime();
                PreOrderCapacityCreation? capacity = null;
                if (product.SaleType == SaleType.PreOrder)
                {
                    var offer = product.PreOrderOffer
                        ?? throw new InvalidOperationException("Pre-order Product has no matching offer.");
                    try
                    {
                        capacity = PreOrderCapacity.Create(
                            Guid.NewGuid(), product.Id, Guid.NewGuid(), offer,
                            "เปิดรอบพรีออเดอร์", $"product:{product.Id:N}", now, actor);
                    }
                    catch (PreOrderCapacityRuleException exception)
                        when (exception.Rule == PreOrderCapacityRule.PreOrderClosed)
                    {
                        return Result<ProductMutationResult>.Failure(ProductErrors.PreOrderClosePassed);
                    }
                }

                try
                {
                    product.Publish(request.ExpectedVersion, now, actor);
                }
                catch (ProductRuleException exception)
                    when (exception.Rule == ProductRule.PreOrderCloseMustBeFuture)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.PreOrderClosePassed);
                }

                if (capacity is not null)
                {
                    session.Add(capacity);
                    intendedEvidence = ProductMutationEvidence.Capture(product, capacity);
                }
                else
                {
                    intendedEvidence = ProductMutationEvidence.Capture(product);
                }
                return Result<ProductMutationResult>.Success(
                    ProductMutationResult.From(product));
            },
            cancellationToken);

        return await commitResolver.ResolveAsync(
            execution,
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "Publish Product verification requires intended evidence."),
                verificationCancellationToken),
            ProductMutationResult.From,
            "Product",
            cancellationToken);
    }

    private static Error? ValidatePublishReadiness(
        ProductReferenceReadiness readiness,
        IReadOnlyCollection<Guid> characterIds)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(characterIds);
        if (!readiness.CategoryIsAllowedSeed)
        {
            throw new InvalidOperationException(
                "A persisted Product references an unsupported category.");
        }

        if (!readiness.BrandExists)
        {
            throw new InvalidOperationException(
                "A persisted Product references a missing Brand.");
        }

        if (!readiness.UniverseExists)
        {
            throw new InvalidOperationException(
                "A persisted Product references a missing Universe.");
        }

        if (!readiness.CharacterIdsAreDistinct)
        {
            throw new InvalidOperationException(
                "A persisted Product contains duplicate Character references.");
        }

        var expectedCharacters = characterIds.Order().ToArray();
        var existingCharacters = readiness.ExistingCharacterIds.Order().ToArray();
        if (!expectedCharacters.SequenceEqual(existingCharacters))
        {
            throw new InvalidOperationException(
                "A persisted Product contains missing or cross-Universe Character references.");
        }

        if (!readiness.BrandIsReady)
        {
            return ProductErrors.PublishBrandUnavailable;
        }

        return readiness.UniverseIsReady
            ? null
            : ProductErrors.PublishUniverseUnavailable;
    }
}
