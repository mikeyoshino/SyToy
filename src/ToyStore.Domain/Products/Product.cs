using ToyStore.Domain.Catalog;

namespace ToyStore.Domain.Products;

public sealed class Product
{
    public const int MaximumImageCount = 8;
    public const int MaximumModelScaleLength = 30;

    private readonly List<ProductImage> _images = [];
    private readonly List<ProductCharacter> _characters = [];
    private InStockOffer? _inStockOffer;
    private PreOrderOffer? _preOrderOffer;
    private long _version = 1;

    private Product()
    {
        DisplayName = null!;
        NormalizedDisplayName = null!;
        EnglishName = null!;
        NormalizedEnglishName = null!;
        Description = null!;
        Slug = null!;
        CreatedBy = null!;
        UpdatedBy = null!;
    }

    private Product(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string? modelScale,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        SaleType saleType,
        InStockOffer? inStockOffer,
        PreOrderOffer? preOrderOffer,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        ValidateCreation(
            id,
            displayName,
            englishName,
            description,
            modelScale,
            slug,
            productCategoryId,
            brandId,
            universeId,
            saleType,
            inStockOffer,
            preOrderOffer,
            createdAtUtc,
            actor);
        var preparedCollections = PrepareCollections(images, characterIds);

        Id = id;
        DisplayName = displayName.Trim();
        NormalizedDisplayName = CatalogNameNormalizer.Normalize(displayName);
        EnglishName = englishName.Trim();
        NormalizedEnglishName = CatalogNameNormalizer.Normalize(englishName);
        Description = description.Trim();
        ModelScale = PrepareModelScale(modelScale);
        Slug = slug;
        ProductCategoryId = productCategoryId;
        BrandId = brandId;
        UniverseId = universeId;
        SaleType = saleType;
        _inStockOffer = inStockOffer;
        _preOrderOffer = preOrderOffer;
        _images.AddRange(preparedCollections.Images.Select((image, index) =>
            CreateImage(image, index)));
        _characters.AddRange(preparedCollections.CharacterIds.Select(
            characterId => ProductCharacter.Create(id, characterId)));
        Status = ProductStatus.Draft;
        CreatedAtUtc = createdAtUtc;
        CreatedBy = actor;
        UpdatedAtUtc = createdAtUtc;
        UpdatedBy = actor;
    }

    public Guid Id { get; private set; }

    public string DisplayName { get; private set; }

    public string NormalizedDisplayName { get; private set; }

    public string EnglishName { get; private set; }

    public string NormalizedEnglishName { get; private set; }

    public string Description { get; private set; }

    public string? ModelScale { get; private set; }

    public string Slug { get; private set; }

    public Guid ProductCategoryId { get; private set; }

    public Guid BrandId { get; private set; }

    public Guid UniverseId { get; private set; }

    public SaleType SaleType { get; private set; }

    public ProductStatus Status { get; private set; }

    public InStockOffer? InStockOffer => _inStockOffer;

    public PreOrderOffer? PreOrderOffer => _preOrderOffer;

    public IReadOnlyList<ProductImage> Images => _images.AsReadOnly();

    public IReadOnlyList<ProductCharacter> Characters => _characters.AsReadOnly();

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public string CreatedBy { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public string UpdatedBy { get; private set; }

    public DateTimeOffset? PublishedAtUtc { get; private set; }

    public string? PublishedBy { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public string? ArchivedBy { get; private set; }

    public long Version => _version;

    public static Product CreateInStock(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        InStockOffer offer,
        DateTimeOffset createdAtUtc,
        string actor,
        string? modelScale = null) => CreateInStock(
            id,
            displayName,
            englishName,
            description,
            slug,
            productCategoryId,
            brandId,
            universeId,
            offer,
            [],
            [],
            createdAtUtc,
            actor,
            modelScale);

    public static Product CreateInStock(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        InStockOffer offer,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds,
        DateTimeOffset createdAtUtc,
        string actor,
        string? modelScale = null) =>
        new(
            id,
            displayName,
            englishName,
            description,
            modelScale,
            slug,
            productCategoryId,
            brandId,
            universeId,
            SaleType.InStock,
            offer,
            null,
            images,
            characterIds,
            createdAtUtc,
            actor);

    public static Product CreatePreOrder(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        PreOrderOffer offer,
        DateTimeOffset createdAtUtc,
        string actor,
        string? modelScale = null) => CreatePreOrder(
            id,
            displayName,
            englishName,
            description,
            slug,
            productCategoryId,
            brandId,
            universeId,
            offer,
            [],
            [],
            createdAtUtc,
            actor,
            modelScale);

    public static Product CreatePreOrder(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        PreOrderOffer offer,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds,
        DateTimeOffset createdAtUtc,
        string actor,
        string? modelScale = null) =>
        new(
            id,
            displayName,
            englishName,
            description,
            modelScale,
            slug,
            productCategoryId,
            brandId,
            universeId,
            SaleType.PreOrder,
            null,
            offer,
            images,
            characterIds,
            createdAtUtc,
            actor);

    public void UpdateDraftInStock(
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        InStockOffer offer,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor,
        string? modelScale = null)
    {
        EnsureDraftInStockEdit();
        EnsureExpectedVersion(expectedVersion);
        ValidateEditableFields(
            displayName,
            englishName,
            description,
            modelScale,
            slug,
            productCategoryId,
            brandId,
            universeId,
            offer);
        var preparedCollections = PrepareCollections(images, characterIds);
        ValidateTouch(changedAtUtc, actor);
        var reconciledCollections = ReconcileCollections(preparedCollections);

        var preparedDisplayName = displayName.Trim();
        var normalizedDisplayName = CatalogNameNormalizer.Normalize(displayName);
        var preparedEnglishName = englishName.Trim();
        var normalizedEnglishName = CatalogNameNormalizer.Normalize(englishName);
        var preparedDescription = description.Trim();
        var preparedModelScale = PrepareModelScale(modelScale);
        if (HasSameDraftInStockContent(
            preparedDisplayName,
            normalizedDisplayName,
            preparedEnglishName,
            normalizedEnglishName,
            preparedDescription,
            preparedModelScale,
            slug,
            productCategoryId,
            brandId,
            universeId,
            offer,
            preparedCollections))
        {
            return;
        }

        EnsureVersionCanAdvance();
        DisplayName = preparedDisplayName;
        NormalizedDisplayName = normalizedDisplayName;
        EnglishName = preparedEnglishName;
        NormalizedEnglishName = normalizedEnglishName;
        Description = preparedDescription;
        ModelScale = preparedModelScale;
        Slug = slug;
        ProductCategoryId = productCategoryId;
        BrandId = brandId;
        UniverseId = universeId;
        _inStockOffer = offer;
        for (var index = 0; index < reconciledCollections.Images.Count; index++)
        {
            reconciledCollections.Images[index].SetSortOrder(index);
        }

        _images.Clear();
        _images.AddRange(reconciledCollections.Images);
        _characters.Clear();
        _characters.AddRange(reconciledCollections.Characters);
        ApplyMutationAudit(changedAtUtc, actor);
    }

    public void UpdateDraftPreOrder(
        string displayName,
        string englishName,
        string description,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        PreOrderOffer offer,
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds,
        long expectedVersion,
        DateTimeOffset changedAtUtc,
        string actor,
        string? modelScale = null)
    {
        if (Status != ProductStatus.Draft)
        {
            throw new ProductRuleException(ProductRule.ProductEditsLocked);
        }

        if (SaleType != SaleType.PreOrder)
        {
            throw new ProductRuleException(ProductRule.ProductPreOrderEditRequired);
        }

        EnsureExpectedVersion(expectedVersion);
        ValidateEditableFields(
            displayName, englishName, description, modelScale, slug,
            productCategoryId, brandId, universeId, offer);
        if (offer.CloseAtUtc <= changedAtUtc)
        {
            throw new ProductRuleException(ProductRule.PreOrderCloseMustBeFuture);
        }

        var preparedCollections = PrepareCollections(images, characterIds);
        ValidateTouch(changedAtUtc, actor);
        var reconciledCollections = ReconcileCollections(preparedCollections);
        EnsureVersionCanAdvance();
        DisplayName = displayName.Trim();
        NormalizedDisplayName = CatalogNameNormalizer.Normalize(displayName);
        EnglishName = englishName.Trim();
        NormalizedEnglishName = CatalogNameNormalizer.Normalize(englishName);
        Description = description.Trim();
        ModelScale = PrepareModelScale(modelScale);
        Slug = slug;
        ProductCategoryId = productCategoryId;
        BrandId = brandId;
        UniverseId = universeId;
        _inStockOffer = null;
        _preOrderOffer = offer;
        for (var index = 0; index < reconciledCollections.Images.Count; index++)
        {
            reconciledCollections.Images[index].SetSortOrder(index);
        }

        _images.Clear();
        _images.AddRange(reconciledCollections.Images);
        _characters.Clear();
        _characters.AddRange(reconciledCollections.Characters);
        ApplyMutationAudit(changedAtUtc, actor);
    }

    public void Publish(long expectedVersion, DateTimeOffset publishedAtUtc, string actor)
    {
        if (Status != ProductStatus.Draft)
        {
            throw new ProductRuleException(ProductRule.ProductTransitionInvalid);
        }

        EnsureExpectedVersion(expectedVersion);

        if (_images.Count == 0)
        {
            throw new ProductRuleException(ProductRule.ProductPublishRequiresImage);
        }

        EnsureMatchingOffer();
        ValidateTouch(publishedAtUtc, actor);
        if (SaleType == SaleType.PreOrder && _preOrderOffer!.CloseAtUtc <= publishedAtUtc)
        {
            throw new ProductRuleException(ProductRule.PreOrderCloseMustBeFuture);
        }

        EnsureVersionCanAdvance();
        Status = ProductStatus.Published;
        PublishedAtUtc = publishedAtUtc;
        PublishedBy = actor;
        ApplyMutationAudit(publishedAtUtc, actor);
    }

    public void Archive(long expectedVersion, DateTimeOffset archivedAtUtc, string actor)
    {
        EnsureInStockLifecycle();
        if (Status != ProductStatus.Published)
        {
            throw new ProductRuleException(ProductRule.ProductTransitionInvalid);
        }

        EnsureExpectedVersion(expectedVersion);
        ValidateTouch(archivedAtUtc, actor);
        EnsureVersionCanAdvance();
        Status = ProductStatus.Archived;
        ArchivedAtUtc = archivedAtUtc;
        ArchivedBy = actor;
        ApplyMutationAudit(archivedAtUtc, actor);
    }

    private static void ValidateCreation(
        Guid id,
        string displayName,
        string englishName,
        string description,
        string? modelScale,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        SaleType saleType,
        InStockOffer? inStockOffer,
        PreOrderOffer? preOrderOffer,
        DateTimeOffset createdAtUtc,
        string actor)
    {
        if (id == Guid.Empty)
        {
            throw new ProductRuleException(ProductRule.ProductIdentityRequired);
        }

        if (string.IsNullOrWhiteSpace(displayName) ||
            string.IsNullOrWhiteSpace(englishName) ||
            string.IsNullOrWhiteSpace(description))
        {
            throw new ProductRuleException(ProductRule.ProductTextRequired);
        }

        _ = PrepareModelScale(modelScale);

        if (!CatalogSlug.IsValid(slug))
        {
            throw new ProductRuleException(ProductRule.ProductSlugInvalid);
        }

        if (productCategoryId == Guid.Empty || brandId == Guid.Empty || universeId == Guid.Empty)
        {
            throw new ProductRuleException(ProductRule.ProductRelationRequired);
        }

        var hasMatchingOffer = saleType switch
        {
            SaleType.InStock => inStockOffer is not null && preOrderOffer is null,
            SaleType.PreOrder => inStockOffer is null && preOrderOffer is not null,
            _ => false,
        };
        if (!hasMatchingOffer)
        {
            throw new ProductRuleException(ProductRule.ProductOfferMismatch);
        }

        EnsureUtc(createdAtUtc);
        if (saleType == SaleType.PreOrder && preOrderOffer!.CloseAtUtc <= createdAtUtc)
        {
            throw new ProductRuleException(ProductRule.PreOrderCloseMustBeFuture);
        }

        EnsureActor(actor);
    }

    private static void ValidateEditableFields(
        string displayName,
        string englishName,
        string description,
        string? modelScale,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        InStockOffer? offer)
    {
        if (string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(englishName)
            || string.IsNullOrWhiteSpace(description))
        {
            throw new ProductRuleException(ProductRule.ProductTextRequired);
        }

        _ = PrepareModelScale(modelScale);

        if (!CatalogSlug.IsValid(slug))
        {
            throw new ProductRuleException(ProductRule.ProductSlugInvalid);
        }

        if (productCategoryId == Guid.Empty || brandId == Guid.Empty || universeId == Guid.Empty)
        {
            throw new ProductRuleException(ProductRule.ProductRelationRequired);
        }

        if (offer is null)
        {
            throw new ProductRuleException(ProductRule.ProductOfferMismatch);
        }
    }

    private static void ValidateEditableFields(
        string displayName,
        string englishName,
        string description,
        string? modelScale,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        PreOrderOffer? offer)
    {
        if (string.IsNullOrWhiteSpace(displayName)
            || string.IsNullOrWhiteSpace(englishName)
            || string.IsNullOrWhiteSpace(description))
        {
            throw new ProductRuleException(ProductRule.ProductTextRequired);
        }

        _ = PrepareModelScale(modelScale);

        if (!CatalogSlug.IsValid(slug))
        {
            throw new ProductRuleException(ProductRule.ProductSlugInvalid);
        }

        if (productCategoryId == Guid.Empty || brandId == Guid.Empty || universeId == Guid.Empty)
        {
            throw new ProductRuleException(ProductRule.ProductRelationRequired);
        }

        if (offer is null)
        {
            throw new ProductRuleException(ProductRule.ProductOfferMismatch);
        }
    }

    private static PreparedCollections PrepareCollections(
        IReadOnlyCollection<ProductImageDefinition> images,
        IReadOnlyCollection<Guid> characterIds)
    {
        var imageSnapshot = images?.ToList()
            ?? throw new ProductRuleException(ProductRule.ProductImageOrderInvalid);
        if (imageSnapshot.Count > MaximumImageCount)
        {
            throw new ProductRuleException(ProductRule.ProductImageLimitExceeded);
        }

        if (imageSnapshot.Any(image => image is null
            || image.Id == Guid.Empty
            || string.IsNullOrWhiteSpace(image.StorageKey)
            || string.IsNullOrWhiteSpace(image.PublicRelativeUrl)
            || string.IsNullOrWhiteSpace(image.AltText)
            || (string.IsNullOrWhiteSpace(image.ThumbnailStorageKey)
                != string.IsNullOrWhiteSpace(image.ThumbnailPublicRelativeUrl))))
        {
            throw new ProductRuleException(ProductRule.ProductImageMetadataRequired);
        }

        if (imageSnapshot.Select(image => image.Id).Distinct().Count() != imageSnapshot.Count)
        {
            throw new ProductRuleException(ProductRule.ProductImageDuplicateId);
        }

        if (imageSnapshot.Select(image => image.StorageKey)
            .Distinct(StringComparer.Ordinal).Count() != imageSnapshot.Count)
        {
            throw new ProductRuleException(ProductRule.ProductImageDuplicateStorageKey);
        }

        var allStorageKeys = imageSnapshot
            .SelectMany(image => image.ThumbnailStorageKey is null
                ? [image.StorageKey]
                : new[] { image.StorageKey, image.ThumbnailStorageKey })
            .ToArray();
        if (allStorageKeys.Distinct(StringComparer.Ordinal).Count() != allStorageKeys.Length)
        {
            throw new ProductRuleException(ProductRule.ProductImageDuplicateStorageKey);
        }

        var characterSnapshot = characterIds?.ToList()
            ?? throw new ProductRuleException(ProductRule.ProductCharacterIdentityRequired);
        if (characterSnapshot.Any(characterId => characterId == Guid.Empty))
        {
            throw new ProductRuleException(ProductRule.ProductCharacterIdentityRequired);
        }

        if (characterSnapshot.Distinct().Count() != characterSnapshot.Count)
        {
            throw new ProductRuleException(ProductRule.ProductCharacterDuplicate);
        }

        return new PreparedCollections(
            imageSnapshot,
            characterSnapshot.Order().ToArray());
    }

    private ReconciledCollections ReconcileCollections(PreparedCollections collections)
    {
        var currentImagesById = _images.ToDictionary(image => image.Id);
        var reconciledImages = new List<ProductImage>(collections.Images.Count);
        for (var index = 0; index < collections.Images.Count; index++)
        {
            var definition = collections.Images[index];
            if (!currentImagesById.TryGetValue(definition.Id, out var retainedImage))
            {
                reconciledImages.Add(CreateImage(definition, index));
                continue;
            }

            if (retainedImage.StorageKey != definition.StorageKey
                || retainedImage.PublicRelativeUrl != definition.PublicRelativeUrl
                || retainedImage.ThumbnailStorageKey != definition.ThumbnailStorageKey
                || retainedImage.ThumbnailPublicRelativeUrl != definition.ThumbnailPublicRelativeUrl
                || retainedImage.AltText != definition.AltText)
            {
                throw new ProductRuleException(
                    ProductRule.ProductImageRetainedMetadataMismatch);
            }

            reconciledImages.Add(retainedImage);
        }

        var currentCharactersById = _characters.ToDictionary(link => link.CharacterId);
        var reconciledCharacters = collections.CharacterIds
            .Select(characterId => currentCharactersById.GetValueOrDefault(characterId)
                ?? ProductCharacter.Create(Id, characterId))
            .ToArray();
        return new ReconciledCollections(reconciledImages, reconciledCharacters);
    }

    private static ProductImage CreateImage(ProductImageDefinition definition, int sortOrder) =>
        new(
            definition.Id,
            definition.StorageKey,
            definition.PublicRelativeUrl,
            definition.AltText,
            sortOrder,
            definition.ThumbnailStorageKey,
            definition.ThumbnailPublicRelativeUrl);

    private static void EnsureUtc(DateTimeOffset instant)
    {
        if (instant.Offset != TimeSpan.Zero)
        {
            throw new ProductRuleException(ProductRule.UtcInstantRequired);
        }
    }

    private static string? PrepareModelScale(string? modelScale)
    {
        if (string.IsNullOrWhiteSpace(modelScale))
        {
            return null;
        }

        var prepared = modelScale.Trim();
        if (prepared.Length > MaximumModelScaleLength
            || prepared.Any(char.IsControl))
        {
            throw new ProductRuleException(ProductRule.ProductModelScaleInvalid);
        }

        return prepared;
    }

    private static void EnsureActor(string actor)
    {
        if (string.IsNullOrWhiteSpace(actor))
        {
            throw new ProductRuleException(ProductRule.ProductActorRequired);
        }
    }

    private void EnsureMatchingOffer()
    {
        var hasMatchingOffer = SaleType switch
        {
            SaleType.InStock => _inStockOffer is not null && _preOrderOffer is null,
            SaleType.PreOrder => _inStockOffer is null && _preOrderOffer is not null,
            _ => false,
        };
        if (!hasMatchingOffer)
        {
            throw new ProductRuleException(ProductRule.ProductOfferMismatch);
        }
    }

    private void EnsureDraftInStockEdit()
    {
        if (Status != ProductStatus.Draft)
        {
            throw new ProductRuleException(ProductRule.ProductEditsLocked);
        }

        if (SaleType != SaleType.InStock)
        {
            throw new ProductRuleException(ProductRule.ProductInStockEditRequired);
        }
    }

    private void EnsureInStockLifecycle()
    {
        if (SaleType != SaleType.InStock)
        {
            throw new ProductRuleException(ProductRule.ProductInStockLifecycleRequired);
        }
    }

    private void EnsureExpectedVersion(long expectedVersion)
    {
        if (expectedVersion != Version)
        {
            throw new ProductRuleException(ProductRule.ProductConcurrencyVersionMismatch);
        }
    }

    private void ValidateTouch(DateTimeOffset changedAtUtc, string actor)
    {
        EnsureUtc(changedAtUtc);
        EnsureActor(actor);
        if (changedAtUtc < UpdatedAtUtc)
        {
            throw new ProductRuleException(ProductRule.ProductAuditTimeWentBackwards);
        }
    }

    private void EnsureVersionCanAdvance()
    {
        if (Version == long.MaxValue)
        {
            throw new ProductRuleException(ProductRule.ProductConcurrencyVersionExhausted);
        }
    }

    private bool HasSameDraftInStockContent(
        string displayName,
        string normalizedDisplayName,
        string englishName,
        string normalizedEnglishName,
        string description,
        string? modelScale,
        string slug,
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        InStockOffer offer,
        PreparedCollections collections) =>
        DisplayName == displayName
        && NormalizedDisplayName == normalizedDisplayName
        && EnglishName == englishName
        && NormalizedEnglishName == normalizedEnglishName
        && Description == description
        && ModelScale == modelScale
        && Slug == slug
        && ProductCategoryId == productCategoryId
        && BrandId == brandId
        && UniverseId == universeId
        && Equals(_inStockOffer, offer)
        && HasSameImages(collections.Images)
        && _characters.Select(link => link.CharacterId).Order()
            .SequenceEqual(collections.CharacterIds);

    private bool HasSameImages(IReadOnlyList<ProductImageDefinition> images)
    {
        if (_images.Count != images.Count)
        {
            return false;
        }

        var currentImages = _images.OrderBy(image => image.SortOrder).ToArray();
        for (var index = 0; index < currentImages.Length; index++)
        {
            var current = currentImages[index];
            var definition = images[index];
            if (current.Id != definition.Id
                || current.StorageKey != definition.StorageKey
                || current.PublicRelativeUrl != definition.PublicRelativeUrl
                || current.ThumbnailStorageKey != definition.ThumbnailStorageKey
                || current.ThumbnailPublicRelativeUrl != definition.ThumbnailPublicRelativeUrl
                || current.AltText != definition.AltText
                || current.SortOrder != index
                || current.IsPrimary != (index == 0))
            {
                return false;
            }
        }

        return true;
    }

    private void ApplyMutationAudit(DateTimeOffset changedAtUtc, string actor)
    {
        UpdatedAtUtc = changedAtUtc;
        UpdatedBy = actor;
        _version++;
    }

    private sealed record PreparedCollections(
        IReadOnlyList<ProductImageDefinition> Images,
        IReadOnlyList<Guid> CharacterIds);

    private sealed record ReconciledCollections(
        IReadOnlyList<ProductImage> Images,
        IReadOnlyList<ProductCharacter> Characters);
}
