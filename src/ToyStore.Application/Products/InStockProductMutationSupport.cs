using ToyStore.Application.Common.Files;
using ToyStore.Application.Common.Models;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

internal static class InStockProductMutationSupport
{
    internal static Error? ValidateReferences(
        ProductReferenceReadiness readiness,
        IReadOnlyCollection<Guid> requestedCharacterIds)
    {
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(requestedCharacterIds);
        if (!readiness.CategoryIsAllowedSeed)
        {
            return ProductErrors.CategoryUnavailable;
        }

        if (!readiness.BrandExists
            || readiness.BrandStatus != CatalogReferenceStatus.Active)
        {
            return ProductErrors.BrandUnavailable;
        }

        if (!readiness.UniverseExists
            || readiness.UniverseStatus != CatalogReferenceStatus.Active)
        {
            return ProductErrors.UniverseUnavailable;
        }

        if (!readiness.CharacterIdsAreDistinct)
        {
            return ProductErrors.DuplicateCharacters;
        }

        var requested = requestedCharacterIds.Distinct().Order().ToArray();
        var existing = readiness.ExistingCharacterIds.Distinct().Order().ToArray();
        return requested.SequenceEqual(existing)
            ? null
            : ProductErrors.CharactersUnavailable;
    }

    internal static Result<IReadOnlyList<ProductImageDefinition>> ResolveCreateImages(
        IReadOnlyList<ResolvedProductMediaSlot> slots,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(slots);
        var definitions = new List<ProductImageDefinition>(slots.Count);
        for (var index = 0; index < slots.Count; index++)
        {
            if (slots[index] is not ResolvedUploadProductMediaSlot upload)
            {
                return Result<IReadOnlyList<ProductImageDefinition>>.Failure(
                    ProductErrors.InvalidMediaPlan);
            }

            definitions.Add(NewImage(upload.Media, displayName, index));
        }

        return Result<IReadOnlyList<ProductImageDefinition>>.Success(definitions);
    }

    internal static Result<IReadOnlyList<ProductImageDefinition>> ResolveUpdateImages(
        IReadOnlyList<ResolvedProductMediaSlot> slots,
        IReadOnlyCollection<ProductImage> currentImages,
        string displayName)
    {
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(currentImages);
        var currentById = currentImages.ToDictionary(image => image.Id);
        var definitions = new List<ProductImageDefinition>(slots.Count);
        for (var index = 0; index < slots.Count; index++)
        {
            switch (slots[index])
            {
                case ResolvedRetainedProductMediaSlot retained
                    when currentById.TryGetValue(retained.ProductImageId, out var current):
                    definitions.Add(new ProductImageDefinition(
                        current.Id,
                        current.StorageKey,
                        current.PublicRelativeUrl,
                        current.AltText,
                        current.ThumbnailStorageKey,
                        current.ThumbnailPublicRelativeUrl));
                    break;
                case ResolvedUploadProductMediaSlot upload:
                    definitions.Add(NewImage(upload.Media, displayName, index));
                    break;
                default:
                    return Result<IReadOnlyList<ProductImageDefinition>>.Failure(
                        ProductErrors.InvalidMediaPlan);
            }
        }

        return Result<IReadOnlyList<ProductImageDefinition>>.Success(definitions);
    }

    internal static bool TryMapProductRule(ProductRule rule, out Error error)
    {
        error = rule switch
        {
            ProductRule.ProductConcurrencyVersionMismatch => ProductErrors.StaleVersion,
            ProductRule.ProductEditsLocked
                or ProductRule.ProductInStockEditRequired
                or ProductRule.ProductInStockLifecycleRequired =>
                    ProductErrors.DraftInStockRequired,
            ProductRule.ProductImageRetainedMetadataMismatch
                or ProductRule.ProductImageNotFound
                or ProductRule.ProductImageDuplicateId
                or ProductRule.ProductImageDuplicateStorageKey
                or ProductRule.ProductImageMetadataRequired
                or ProductRule.ProductImageLimitExceeded
                or ProductRule.ProductImageOrderInvalid => ProductErrors.InvalidMediaPlan,
            ProductRule.ProductCharacterDuplicate => ProductErrors.DuplicateCharacters,
            ProductRule.MoneyCannotBeNegative
                or ProductRule.InStockPriceMustBePositive
                or ProductRule.ProductTextRequired
                or ProductRule.ProductRelationRequired
                or ProductRule.ProductSlugInvalid
                or ProductRule.ProductCharacterIdentityRequired => ProductErrors.InvalidInput,
            _ => Error.None,
        };
        return error != Error.None;
    }

    internal static bool TryMapInventoryRule(InventoryRule rule, out Error error)
    {
        error = rule == InventoryRule.QuantityCannotBeNegative
            ? ProductErrors.InvalidInput
            : Error.None;
        return error != Error.None;
    }

    internal static Result<ProductMutationResult> MapMediaFailure(
        Result<ProductMutationResult> result,
        string propertyName)
    {
        if (result.IsSuccess
            || result.Error.Type != ErrorType.Validation
            || result.ValidationFailures.Count != 0
            || (result.Error != ProductErrors.InvalidMediaPlan
                && !result.Error.Code.StartsWith("Media.", StringComparison.Ordinal)))
        {
            return result;
        }

        return Result<ProductMutationResult>.Failure(
            result.Error,
            [new FieldValidationFailure(propertyName, result.Error.Message)]);
    }

    private static ProductImageDefinition NewImage(
        StagedMedia media,
        string displayName,
        int index) => new(
            Guid.NewGuid(),
            media.StorageKey,
            media.PublicRelativeUrl,
            $"รูปสินค้า {displayName.Trim()} {index + 1}",
            media.ThumbnailStorageKey,
            media.ThumbnailPublicRelativeUrl);
}
