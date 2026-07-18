using MediatR;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products.CreateInStockProduct;

public sealed class CreateInStockProductHandler(
    IProductMutationSessionFactory sessionFactory,
    ProductMediaMutationCoordinator mediaCoordinator,
    TimeProvider timeProvider)
    : IRequestHandler<CreateInStockProductCommand, Result<ProductMutationResult>>
{
    public async Task<Result<ProductMutationResult>> Handle(
        CreateInStockProductCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var actor = request.AuthorizedActorId
            ?? throw new InvalidOperationException(
                "An authorized actor is required before creating a Product.");
        var imagePlan = request.Images?.ToArray()
            ?? throw new InvalidOperationException(
                "Create Product validation must provide an image plan before the handler.");
        var characterIds = request.CharacterIds?.ToArray()
            ?? throw new InvalidOperationException(
                "Create Product validation must provide characters before the handler.");
        var productId = Guid.NewGuid();
        var inventoryItemId = Guid.NewGuid();
        var initialMovementId = Guid.NewGuid();
        ProductMutationEvidence? intendedEvidence = null;
        ProductMediaMutationState? mediaState = null;
        var beforeMedia = ProductMediaSnapshot.Capture(Array.Empty<ProductImage>());

        await using var session = await sessionFactory.OpenAsync(cancellationToken);
        var result = await mediaCoordinator.ExecuteAsync<
            ProductMutationResult,
            ProductMutationEvidence>(
            imagePlan,
            session,
            async (resolvedSlots, operationCancellationToken) =>
            {
                await session.AcquireNamespaceLockAsync(operationCancellationToken);
                if (await session.LockProductAsync(productId, operationCancellationToken) is not null)
                {
                    throw new InvalidOperationException(
                        "A generated Product identity unexpectedly already exists.");
                }

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
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.DuplicateDisplayName);
                }

                if (await session.EnglishNameExistsAsync(
                        CatalogNameNormalizer.Normalize(request.EnglishName),
                        excludedId: null,
                        operationCancellationToken))
                {
                    return Result<ProductMutationResult>.Failure(
                        ProductErrors.DuplicateEnglishName);
                }

                var slug = await session.AllocateSlugAsync(
                    request.EnglishName,
                    excludedId: null,
                    operationCancellationToken);
                var images = InStockProductMutationSupport.ResolveCreateImages(
                    resolvedSlots,
                    request.DisplayName);
                if (images.IsFailure)
                {
                    return Result<ProductMutationResult>.Failure(images.Error);
                }

                try
                {
                    var now = timeProvider.GetUtcNow().ToUniversalTime();
                    var product = Product.CreateInStock(
                        productId,
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
                        now,
                        actor,
                        request.ModelScale);
                    var inventory = InventoryItem.Create(
                        inventoryItemId,
                        product.Id,
                        initialMovementId,
                        request.InitialStock,
                        "สร้างสินค้าและกำหนดสต็อกเริ่มต้น",
                        $"product:{product.Id:N}",
                        now,
                        actor);
                    session.Add(product, inventory);
                    intendedEvidence = ProductMutationEvidence.Capture(product, inventory);
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
                catch (InventoryRuleException exception)
                    when (InStockProductMutationSupport.TryMapInventoryRule(
                        exception.Rule,
                        out _))
                {
                    _ = InStockProductMutationSupport.TryMapInventoryRule(
                        exception.Rule,
                        out var error);
                    return Result<ProductMutationResult>.Failure(error);
                }
            },
            verificationCancellationToken => sessionFactory.VerifyCommitAsync(
                intendedEvidence ?? throw new InvalidOperationException(
                    "Create Product verification requires intended evidence."),
                verificationCancellationToken),
            ProductMutationResult.From,
            new ProductMediaMutationContext(productId),
            () => mediaState ?? throw new InvalidOperationException(
                "Create Product media state was not captured."),
            cancellationToken);

        return InStockProductMutationSupport.MapMediaFailure(
            result,
            nameof(CreateInStockProductCommand.Images));
    }
}
