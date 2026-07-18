using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.UpdateDraftInStockProduct;

public sealed class UpdateDraftInStockProductHandler(
    IProductMutationSessionFactory sessionFactory,
    ProductMediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateDraftInStockProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(
        UpdateDraftInStockProductCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before updating a Product.");
        var imagePlan = request.Images?.ToArray()
            ?? throw new InvalidOperationException(
                "Update Product validation must provide an image plan before the handler.");
        var characterIds = request.CharacterIds?.ToArray()
            ?? throw new InvalidOperationException(
                "Update Product validation must provide characters before the handler.");
        ProductMutationEvidence? intendedEvidence = null;
        ProductMediaMutationState? mediaState = null;

        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var result = await mediaCoordinator.ExecuteAsync<
            ProductMutationResult,
            ProductMutationEvidence>(
            imagePlan,
            session,
            async (resolvedSlots, operationCancellationToken) =>
            {
                await session.AcquireNamespaceLockAsync(operationCancellationToken);
                var product = await session.LockProductAsync(
                    request.Id,
                    operationCancellationToken);
                if (product is null)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.NotFound);
                }

                if (product.Status != ProductStatus.Draft
                    || product.SaleType != SaleType.InStock)
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.DraftInStockRequired);
                }

                if (product.Version != request.ExpectedVersion)
                {
                    return Result<ProductMutationResult>.Failure(ProductErrors.StaleVersion);
                }

                var beforeMedia = ProductMediaSnapshot.Capture(product.Images);
                var readiness = await session.LockReferencesAsync(
                    request.ProductCategoryId,
                    request.BrandId,
                    request.UniverseId,
                    characterIds,
                    operationCancellationToken);
                var referenceError = InStockProductMutationSupport.ValidateReferences(
                    readiness,
                    characterIds);
                if (referenceError is not null)
                {
                    return Result<ProductMutationResult>.Failure(referenceError);
                }

                if (await session.DisplayNameExistsAsync(
                        CatalogNameNormalizer.Normalize(request.DisplayName),
                        product.Id,
                        operationCancellationToken))
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.DuplicateDisplayName);
                }

                if (await session.EnglishNameExistsAsync(
                        CatalogNameNormalizer.Normalize(request.EnglishName),
                        product.Id,
                        operationCancellationToken))
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.DuplicateEnglishName);
                }

                var persistedEnglishName = request.EnglishName.Trim();
                var slug = string.Equals(
                    product.EnglishName,
                    persistedEnglishName,
                    StringComparison.Ordinal)
                    ? CatalogSlug.Create(product.Slug)
                    : await session.AllocateSlugAsync(
                        request.EnglishName,
                        product.Id,
                        operationCancellationToken);
                var images = InStockProductMutationSupport.ResolveUpdateImages(
                    resolvedSlots,
                    product.Images,
                    request.DisplayName);
                if (images.IsFailure)
                {
                    return Result<ProductMutationResult>.Failure(images.Error);
                }

                try
                {
                    product.UpdateDraftInStock(
                        request.DisplayName,
                        request.EnglishName,
                        request.Description,
                        slug.Value,
                        request.ProductCategoryId,
                        request.BrandId,
                        request.UniverseId,
                        InStockOffer.Create(Money.Create(request.Price)),
                        images.Value,
                        characterIds,
                        request.ExpectedVersion,
                        timeProvider.GetUtcNow().ToUniversalTime(),
                        actor,
                        request.ModelScale);
                    intendedEvidence = ProductMutationEvidence.Capture(product);
                    mediaState = ProductMediaMutationState.Capture(beforeMedia, product.Images);
                    return Result<ProductMutationResult>.Success(
                        ProductMutationResult.From(product));
                }
                catch (ProductRuleException exception)
                    when (InStockProductMutationSupport.TryMapProductRule(
                        exception.Rule,
                        out _))
                {
                    _ = InStockProductMutationSupport.TryMapProductRule(
                        exception.Rule,
                        out var error);
                    return Result<ProductMutationResult>.Failure(
                        error);
                }
            },
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "Update Product verification requires intended evidence."),
                verificationCancellationToken),
            ProductMutationResult.From,
            new ProductMediaMutationContext(request.Id),
            () => mediaState ?? throw new InvalidOperationException(
                "Update Product media state was not captured."),
            cancellationToken);

        return InStockProductMutationSupport.MapMediaFailure(
            result,
            nameof(UpdateDraftInStockProductCommand.Images));
    }
}
