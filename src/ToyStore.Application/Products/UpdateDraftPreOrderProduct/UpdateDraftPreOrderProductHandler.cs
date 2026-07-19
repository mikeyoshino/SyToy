using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.UpdateDraftPreOrderProduct;

public sealed class UpdateDraftPreOrderProductHandler(
    IProductMutationSessionFactory sessionFactory,
    ProductMediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<UpdateDraftPreOrderProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(
        UpdateDraftPreOrderProductCommand request,
        CancellationToken cancellationToken)
    {
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException("Pre-order Product update requires Admin authorization.");
        var imagePlan = request.Images?.ToArray()
            ?? throw new InvalidOperationException("Product validation must provide an image plan.");
        var characterIds = request.CharacterIds?.ToArray()
            ?? throw new InvalidOperationException("Product validation must provide characters.");
        ProductMutationEvidence? evidence = null;
        ProductMediaMutationState? mediaState = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var result = await mediaCoordinator.ExecuteAsync<ProductMutationResult, ProductMutationEvidence>(
            imagePlan,
            session,
            async (resolved, token) =>
            {
                await session.AcquireNamespaceLockAsync(token);
                var product = await session.LockProductAsync(request.Id, token);
                if (product is null) return Result<ProductMutationResult>.Failure(ProductErrors.NotFound);
                if (product.Status is not (ProductStatus.Draft or ProductStatus.Published)
                    || product.SaleType != SaleType.PreOrder)
                    return Result<ProductMutationResult>.Failure(ProductErrors.EditablePreOrderRequired);
                if (product.Version != request.ExpectedVersion)
                    return Result<ProductMutationResult>.Failure(ProductErrors.StaleVersion);

                var beforeMedia = ProductMediaSnapshot.Capture(product.Images);
                var readiness = await session.LockReferencesAsync(
                    request.ProductCategoryId, request.BrandId, request.UniverseId, characterIds, token);
                var referenceError = InStockProductMutationSupport.ValidateReferences(readiness, characterIds);
                if (referenceError is not null) return Result<ProductMutationResult>.Failure(referenceError);
                if (product.Status == ProductStatus.Published && !readiness.BrandIsReady)
                    return Result<ProductMutationResult>.Failure(ProductErrors.PublishBrandUnavailable);
                if (product.Status == ProductStatus.Published && !readiness.UniverseIsReady)
                    return Result<ProductMutationResult>.Failure(ProductErrors.PublishUniverseUnavailable);
                if (await session.DisplayNameExistsAsync(
                    CatalogNameNormalizer.Normalize(request.DisplayName), product.Id, token))
                    return Result<ProductMutationResult>.Failure(ProductErrors.DuplicateDisplayName);
                if (await session.EnglishNameExistsAsync(
                    CatalogNameNormalizer.Normalize(request.EnglishName), product.Id, token))
                    return Result<ProductMutationResult>.Failure(ProductErrors.DuplicateEnglishName);

                var persistedEnglishName = request.EnglishName.Trim();
                var slug = string.Equals(product.EnglishName, persistedEnglishName, StringComparison.Ordinal)
                    ? CatalogSlug.Create(product.Slug)
                    : await session.AllocateSlugAsync(request.EnglishName, product.Id, token);
                var images = InStockProductMutationSupport.ResolveUpdateImages(
                    resolved, product.Images, request.DisplayName);
                if (images.IsFailure) return Result<ProductMutationResult>.Failure(images.Error);
                if (product.Status == ProductStatus.Published && images.Value.Count == 0)
                    return Result<ProductMutationResult>.Failure(ProductErrors.PublishRequiresImage);
                try
                {
                    var now = timeProvider.GetUtcNow().ToUniversalTime();
                    var offer = PreOrderOffer.Create(
                        Money.Create(request.FullPrice), Money.Create(request.DepositAmount),
                        request.CloseDate,
                        EstimatedArrival.Create(request.EstimatedArrivalMonth, request.EstimatedArrivalYear),
                        request.TotalCapacity, request.MaxPerCustomer, now, request.BalancePaymentDays);
                    if (product.Status == ProductStatus.Published
                        && (offer.CloseAtUtc != product.PreOrderOffer!.CloseAtUtc
                            || offer.TotalCapacity != product.PreOrderOffer.TotalCapacity))
                    {
                        return Result<ProductMutationResult>.Failure(
                            ProductErrors.PublishedPreOrderCapacityLocked);
                    }
                    product.UpdateDraftPreOrder(
                        request.DisplayName, request.EnglishName, request.Description, slug.Value,
                        request.ProductCategoryId, request.BrandId, request.UniverseId, offer,
                        images.Value, characterIds, request.ExpectedVersion, now, actor,
                        request.ModelScale);
                    evidence = ProductMutationEvidence.Capture(product);
                    mediaState = ProductMediaMutationState.Capture(beforeMedia, product.Images);
                    return Result<ProductMutationResult>.Success(ProductMutationResult.From(product));
                }
                catch (ProductRuleException exception)
                    when (PreOrderProductValidationRules.TryMapProductRule(
                        exception.Rule, out _, out _))
                {
                    _ = PreOrderProductValidationRules.TryMapProductRule(
                        exception.Rule, out var error, out var failure);
                    return Result<ProductMutationResult>.Failure(
                        error,
                        failure is null ? null : [failure]);
                }
            },
            token => sessionFactory.VerifyCommitAsync(
                evidence ?? throw new InvalidOperationException("Missing update evidence."), token),
            ProductMutationResult.From,
            new ProductMediaMutationContext(request.Id),
            () => mediaState ?? throw new InvalidOperationException("Missing update media state."),
            cancellationToken);
        return InStockProductMutationSupport.MapMediaFailure(
            result, nameof(UpdateDraftPreOrderProductCommand.Images));
    }
}
