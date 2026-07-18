using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.CreatePreOrderProduct;

public sealed class CreatePreOrderProductHandler(
    IProductMutationSessionFactory sessionFactory,
    ProductMediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePreOrderProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(CreatePreOrderProductCommand request, CancellationToken cancellationToken)
    {
        var actor = request.AuthorizedActorId ?? throw new InvalidOperationException("Pre-order Product create requires Admin authorization.");
        var productId = Guid.NewGuid();
        ProductMutationEvidence? evidence = null;
        ProductMediaMutationState? mediaState = null;
        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var result = await mediaCoordinator.ExecuteAsync<ProductMutationResult, ProductMutationEvidence>(
            request.Images,
            session,
            async (resolved, token) =>
            {
                await session.AcquireNamespaceLockAsync(token);
                _ = await session.LockProductAsync(productId, token);
                var readiness = await session.LockReferencesAsync(
                    request.ProductCategoryId, request.BrandId, request.UniverseId,
                    request.CharacterIds, token);
                var referenceError = InStockProductMutationSupport.ValidateReferences(readiness, request.CharacterIds);
                if (referenceError is not null) return Result<ProductMutationResult>.Failure(referenceError);
                if (await session.DisplayNameExistsAsync(CatalogNameNormalizer.Normalize(request.DisplayName), null, token))
                    return Result<ProductMutationResult>.Failure(ProductErrors.DuplicateDisplayName);
                if (await session.EnglishNameExistsAsync(CatalogNameNormalizer.Normalize(request.EnglishName), null, token))
                    return Result<ProductMutationResult>.Failure(ProductErrors.DuplicateEnglishName);
                var slug = await session.AllocateSlugAsync(request.EnglishName, null, token);
                var images = InStockProductMutationSupport.ResolveCreateImages(resolved, request.DisplayName);
                if (images.IsFailure) return Result<ProductMutationResult>.Failure(images.Error);
                try
                {
                    var now = timeProvider.GetUtcNow().ToUniversalTime();
                    var offer = PreOrderOffer.Create(
                        Money.Create(request.FullPrice), Money.Create(request.DepositAmount),
                        request.CloseDate,
                        EstimatedArrival.Create(request.EstimatedArrivalMonth, request.EstimatedArrivalYear),
                        request.TotalCapacity, request.MaxPerCustomer, now, request.BalancePaymentDays);
                    var product = Product.CreatePreOrder(
                        productId, request.DisplayName, request.EnglishName, request.Description,
                        slug.Value, request.ProductCategoryId, request.BrandId, request.UniverseId,
                        offer, images.Value, request.CharacterIds, now, actor,
                        request.ModelScale);
                    session.Add(product);
                    evidence = ProductMutationEvidence.Capture(product);
                    mediaState = ProductMediaMutationState.Capture(ProductMediaSnapshot.Capture([]), product.Images);
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
            token => sessionFactory.VerifyCommitAsync(evidence ?? throw new InvalidOperationException("Missing create evidence."), token),
            ProductMutationResult.From,
            new ProductMediaMutationContext(productId),
            () => mediaState ?? throw new InvalidOperationException("Missing media state."),
            cancellationToken);
        return InStockProductMutationSupport.MapMediaFailure(result, nameof(request.Images));
    }
}
