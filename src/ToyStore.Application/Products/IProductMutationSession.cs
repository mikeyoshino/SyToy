using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Inventory;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Application.Products;

public interface IProductMutationSessionFactory
{
    ValueTask<IProductMutationSession> OpenAsync(CancellationToken cancellationToken);

    Task<CatalogCommitVerification<ProductMutationEvidence>> VerifyCommitAsync(
        ProductMutationEvidence evidence,
        CancellationToken cancellationToken);
}

public interface IProductMutationSession : ICatalogMutationSession
{
    Task AcquireNamespaceLockAsync(CancellationToken cancellationToken);

    Task<Product?> LockProductAsync(Guid productId, CancellationToken cancellationToken);

    Task<PreOrderCapacity?> LockPreOrderCapacityAsync(
        Guid productId,
        CancellationToken cancellationToken) =>
        throw new NotSupportedException(
            "This Product mutation session does not support Pre-order capacity locking.");

    Task<ProductReferenceReadiness> LockReferencesAsync(
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken cancellationToken);

    Task<bool> DisplayNameExistsAsync(
        string normalizedDisplayName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task<bool> EnglishNameExistsAsync(
        string normalizedEnglishName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    Task<CatalogSlug> AllocateSlugAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken);

    void Add(Product product, InventoryCreation inventoryCreation);

    void Add(Product product) =>
        throw new NotSupportedException("This Product mutation session does not support Product creation without Inventory.");

    void Add(PreOrderCapacityCreation capacityCreation) =>
        throw new NotSupportedException("This Product mutation session does not support Pre-order capacity creation.");

    void Add(PreOrderCapacityMovement movement) =>
        throw new NotSupportedException("This Product mutation session does not support Pre-order capacity adjustment.");
}

public sealed record ProductReferenceReadiness(
    bool CategoryIsAllowedSeed,
    bool BrandExists,
    CatalogReferenceStatus? BrandStatus,
    bool BrandHasImage,
    bool UniverseExists,
    CatalogReferenceStatus? UniverseStatus,
    bool UniverseHasLogo,
    bool CharacterIdsAreDistinct,
    IReadOnlyList<Guid> ExistingCharacterIds)
{
    public bool BrandIsReady =>
        BrandExists && BrandStatus == CatalogReferenceStatus.Active && BrandHasImage;

    public bool UniverseIsReady =>
        UniverseExists && UniverseStatus == CatalogReferenceStatus.Active && UniverseHasLogo;
}

public sealed record ProductImageEvidence(
    Guid Id,
    string StorageKey,
    string PublicRelativeUrl,
    string? ThumbnailStorageKey,
    string? ThumbnailPublicRelativeUrl,
    string AltText,
    int SortOrder,
    bool IsPrimary);

public sealed record PreOrderCapacityCreationEvidence(
    Guid CapacityId,
    Guid ProductId,
    int TotalCapacity,
    int HeldQuantity,
    int CommittedQuantity,
    int RetiredQuantity,
    DateTimeOffset CloseAtUtc,
    long CapacityVersion,
    DateTimeOffset CapacityCreatedAtUtc,
    string CapacityCreatedBy,
    DateTimeOffset CapacityUpdatedAtUtc,
    string CapacityUpdatedBy,
    Guid MovementId,
    PreOrderCapacityMovementType MovementType,
    int MovementQuantity,
    int MovementAvailableDelta,
    int MovementResultingRemaining,
    long MovementResultingVersion,
    string MovementReason,
    string MovementReference,
    string MovementActor,
    DateTimeOffset MovementOccurredAtUtc);

public sealed class ProductMutationEvidence
{
    private ProductMutationEvidence(
        Product product,
        InventoryMutationEvidence? inventoryEvidence,
        DateTimeOffset? inventoryCreatedAtUtc,
        string? inventoryCreatedBy,
        PreOrderCapacityCreationEvidence? preOrderCapacityEvidence)
    {
        Id = product.Id;
        IntendedVersion = product.Version;
        DisplayName = product.DisplayName;
        NormalizedDisplayName = product.NormalizedDisplayName;
        EnglishName = product.EnglishName;
        NormalizedEnglishName = product.NormalizedEnglishName;
        Description = product.Description;
        ModelScale = product.ModelScale;
        Slug = product.Slug;
        ProductCategoryId = product.ProductCategoryId;
        BrandId = product.BrandId;
        UniverseId = product.UniverseId;
        SaleType = product.SaleType;
        Status = product.Status;
        InStockPrice = product.InStockOffer?.Price.Amount;
        PreOrderFullPrice = product.PreOrderOffer?.FullPrice.Amount;
        PreOrderDepositAmount = product.PreOrderOffer?.DepositAmount.Amount;
        PreOrderCloseAtUtc = NormalizeNullable(product.PreOrderOffer?.CloseAtUtc);
        PreOrderEstimatedArrivalMonth = product.PreOrderOffer?.EstimatedArrival.Month;
        PreOrderEstimatedArrivalYear = product.PreOrderOffer?.EstimatedArrival.Year;
        PreOrderTotalCapacity = product.PreOrderOffer?.TotalCapacity;
        PreOrderMaxPerCustomer = product.PreOrderOffer?.MaxPerCustomer;
        PreOrderBalancePaymentDays = product.PreOrderOffer?.BalancePaymentDays;
        CreatedAtUtc = Normalize(product.CreatedAtUtc);
        CreatedBy = product.CreatedBy;
        UpdatedAtUtc = Normalize(product.UpdatedAtUtc);
        UpdatedBy = product.UpdatedBy;
        PublishedAtUtc = NormalizeNullable(product.PublishedAtUtc);
        PublishedBy = product.PublishedBy;
        ArchivedAtUtc = NormalizeNullable(product.ArchivedAtUtc);
        ArchivedBy = product.ArchivedBy;
        Images = product.Images
            .OrderBy(image => image.SortOrder)
            .Select(image => new ProductImageEvidence(
                image.Id,
                image.StorageKey,
                image.PublicRelativeUrl,
                image.ThumbnailStorageKey,
                image.ThumbnailPublicRelativeUrl,
                image.AltText,
                image.SortOrder,
                image.IsPrimary))
            .ToArray();
        CharacterIds = product.Characters
            .Select(link => link.CharacterId)
            .Order()
            .ToArray();
        this.inventoryEvidence = inventoryEvidence;
        this.inventoryCreatedAtUtc = inventoryCreatedAtUtc.HasValue
            ? Normalize(inventoryCreatedAtUtc.Value)
            : null;
        this.inventoryCreatedBy = inventoryCreatedBy;
        this.preOrderCapacityEvidence = preOrderCapacityEvidence;
    }

    private readonly InventoryMutationEvidence? inventoryEvidence;
    private readonly DateTimeOffset? inventoryCreatedAtUtc;
    private readonly string? inventoryCreatedBy;
    private readonly PreOrderCapacityCreationEvidence? preOrderCapacityEvidence;

    public Guid Id { get; }
    public long IntendedVersion { get; }
    public string DisplayName { get; }
    public string NormalizedDisplayName { get; }
    public string EnglishName { get; }
    public string NormalizedEnglishName { get; }
    public string Description { get; }
    public string? ModelScale { get; }
    public string Slug { get; }
    public Guid ProductCategoryId { get; }
    public Guid BrandId { get; }
    public Guid UniverseId { get; }
    public SaleType SaleType { get; }
    public ProductStatus Status { get; }
    public decimal? InStockPrice { get; }
    public decimal? PreOrderFullPrice { get; }
    public decimal? PreOrderDepositAmount { get; }
    public DateTimeOffset? PreOrderCloseAtUtc { get; }
    public int? PreOrderEstimatedArrivalMonth { get; }
    public int? PreOrderEstimatedArrivalYear { get; }
    public int? PreOrderTotalCapacity { get; }
    public int? PreOrderMaxPerCustomer { get; }
    public int? PreOrderBalancePaymentDays { get; }
    public DateTimeOffset CreatedAtUtc { get; }
    public string CreatedBy { get; }
    public DateTimeOffset UpdatedAtUtc { get; }
    public string UpdatedBy { get; }
    public DateTimeOffset? PublishedAtUtc { get; }
    public string? PublishedBy { get; }
    public DateTimeOffset? ArchivedAtUtc { get; }
    public string? ArchivedBy { get; }
    public IReadOnlyList<ProductImageEvidence> Images { get; }
    public IReadOnlyList<Guid> CharacterIds { get; }
    public bool HasInventoryCreation => inventoryEvidence is not null;
    public InventoryMutationEvidence InventoryEvidence => inventoryEvidence
        ?? throw new InvalidOperationException("This Product evidence has no Inventory creation.");
    public DateTimeOffset InventoryCreatedAtUtc => inventoryCreatedAtUtc
        ?? throw new InvalidOperationException("This Product evidence has no Inventory creation.");
    public string InventoryCreatedBy => inventoryCreatedBy
        ?? throw new InvalidOperationException("This Product evidence has no Inventory creation.");
    public bool HasPreOrderCapacityCreation => preOrderCapacityEvidence is not null;
    public PreOrderCapacityCreationEvidence PreOrderCapacityEvidence => preOrderCapacityEvidence
        ?? throw new InvalidOperationException("This Product evidence has no Pre-order capacity creation.");

    public static ProductMutationEvidence Capture(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);
        return new ProductMutationEvidence(product, null, null, null, null);
    }

    public static ProductMutationEvidence Capture(
        Product product,
        InventoryCreation inventoryCreation)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(inventoryCreation);
        return Capture(
            product,
            inventoryCreation.Item,
            inventoryCreation.InitialMovement);
    }

    public static ProductMutationEvidence Capture(
        Product product,
        InventoryItem inventoryItem,
        StockMovement initialMovement)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(inventoryItem);
        ArgumentNullException.ThrowIfNull(initialMovement);
        if (inventoryItem.ProductId != product.Id
            || initialMovement.ProductId != product.Id
            || initialMovement.InventoryItemId != inventoryItem.Id
            || initialMovement.Type != StockMovementType.InitialStock)
        {
            throw new ArgumentException(
                "Product creation evidence must own its Inventory and InitialStock movement.",
                nameof(inventoryItem));
        }

        return new ProductMutationEvidence(
            product,
            InventoryMutationEvidence.Capture(inventoryItem, initialMovement),
            inventoryItem.CreatedAtUtc,
            inventoryItem.CreatedBy,
            null);
    }

    public static ProductMutationEvidence Capture(
        Product product,
        PreOrderCapacityCreation capacityCreation) => Capture(
            product,
            capacityCreation.Capacity,
            capacityCreation.Movement);

    public static ProductMutationEvidence Capture(
        Product product,
        PreOrderCapacity capacity,
        PreOrderCapacityMovement movement)
    {
        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(capacity);
        ArgumentNullException.ThrowIfNull(movement);
        if (capacity.ProductId != product.Id
            || movement.ProductId != product.Id
            || movement.CapacityId != capacity.Id
            || movement.Type != PreOrderCapacityMovementType.InitialCapacity)
        {
            throw new ArgumentException("Pre-order capacity evidence must belong to Product.", nameof(capacity));
        }

        return new ProductMutationEvidence(product, null, null, null, new(
            capacity.Id, capacity.ProductId, capacity.TotalCapacity,
            capacity.HeldQuantity, capacity.CommittedQuantity, capacity.RetiredQuantity,
            Normalize(capacity.CloseAtUtc), capacity.Version,
            Normalize(capacity.CreatedAtUtc), capacity.CreatedBy,
            Normalize(capacity.UpdatedAtUtc), capacity.UpdatedBy,
            movement.Id, movement.Type, movement.Quantity, movement.AvailableQuantityDelta,
            movement.ResultingRemainingQuantity, movement.ResultingCapacityVersion,
            movement.Reason, movement.Reference, movement.Actor,
            Normalize(movement.OccurredAtUtc)));
    }

    public bool ExactlyMatches(ProductMutationEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return ProductSnapshotExactlyMatches(other)
            && HasInventoryCreation == other.HasInventoryCreation
            && HasPreOrderCapacityCreation == other.HasPreOrderCapacityCreation
            && (!HasInventoryCreation || (InventoryCreationProofExactlyMatches(other)
                && InventoryCurrentStateExactlyMatches(other)))
            && (!HasPreOrderCapacityCreation
                || (PreOrderCapacityCreationExactlyMatches(other)
                    && PreOrderCapacityCurrentStateExactlyMatches(other)));
    }

    public bool ProductSnapshotExactlyMatches(ProductMutationEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Id == other.Id
            && IntendedVersion == other.IntendedVersion
            && DisplayName == other.DisplayName
            && NormalizedDisplayName == other.NormalizedDisplayName
            && EnglishName == other.EnglishName
            && NormalizedEnglishName == other.NormalizedEnglishName
            && Description == other.Description
            && ModelScale == other.ModelScale
            && Slug == other.Slug
            && ProductCategoryId == other.ProductCategoryId
            && BrandId == other.BrandId
            && UniverseId == other.UniverseId
            && SaleType == other.SaleType
            && Status == other.Status
            && InStockPrice == other.InStockPrice
            && PreOrderFullPrice == other.PreOrderFullPrice
            && PreOrderDepositAmount == other.PreOrderDepositAmount
            && PreOrderCloseAtUtc == other.PreOrderCloseAtUtc
            && PreOrderEstimatedArrivalMonth == other.PreOrderEstimatedArrivalMonth
            && PreOrderEstimatedArrivalYear == other.PreOrderEstimatedArrivalYear
            && PreOrderTotalCapacity == other.PreOrderTotalCapacity
            && PreOrderMaxPerCustomer == other.PreOrderMaxPerCustomer
            && PreOrderBalancePaymentDays == other.PreOrderBalancePaymentDays
            && CreatedAtUtc == other.CreatedAtUtc
            && CreatedBy == other.CreatedBy
            && UpdatedAtUtc == other.UpdatedAtUtc
            && UpdatedBy == other.UpdatedBy
            && PublishedAtUtc == other.PublishedAtUtc
            && PublishedBy == other.PublishedBy
            && ArchivedAtUtc == other.ArchivedAtUtc
            && ArchivedBy == other.ArchivedBy
            && Images.SequenceEqual(other.Images)
            && CharacterIds.SequenceEqual(other.CharacterIds);
    }

    public bool InventoryCreationProofExactlyMatches(ProductMutationEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!HasInventoryCreation || !other.HasInventoryCreation)
        {
            return false;
        }

        var first = InventoryEvidence;
        var second = other.InventoryEvidence;
        return InventoryCreatedAtUtc == other.InventoryCreatedAtUtc
            && InventoryCreatedBy == other.InventoryCreatedBy
            && first.OperationId == second.OperationId
            && first.InventoryItemId == second.InventoryItemId
            && first.ProductId == second.ProductId
            && first.MovementType == second.MovementType
            && first.QuantityDelta == second.QuantityDelta
            && first.ResultingOnHandQuantity == second.ResultingOnHandQuantity
            && first.ResultingInventoryVersion == second.ResultingInventoryVersion
            && first.Reason == second.Reason
            && first.Reference == second.Reference
            && first.Actor == second.Actor
            && first.OccurredAtUtc == second.OccurredAtUtc
            && first.ReservationId == second.ReservationId;
    }

    public bool InventoryCurrentStateExactlyMatches(ProductMutationEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!HasInventoryCreation || !other.HasInventoryCreation)
        {
            return false;
        }

        var first = InventoryEvidence;
        var second = other.InventoryEvidence;
        return first.InventoryItemId == second.InventoryItemId
            && first.ProductId == second.ProductId
            && first.IntendedOnHandQuantity == second.IntendedOnHandQuantity
            && first.IntendedHeldQuantity == second.IntendedHeldQuantity
            && first.IntendedVersion == second.IntendedVersion
            && first.IntendedUpdatedAtUtc == second.IntendedUpdatedAtUtc
            && first.IntendedUpdatedBy == second.IntendedUpdatedBy;
    }

    public bool PreOrderCapacityCreationExactlyMatches(ProductMutationEvidence other)
    {
        ArgumentNullException.ThrowIfNull(other);
        if (!HasPreOrderCapacityCreation || !other.HasPreOrderCapacityCreation) return false;
        var first = PreOrderCapacityEvidence;
        var second = other.PreOrderCapacityEvidence;
        return first.CapacityId == second.CapacityId
            && first.ProductId == second.ProductId
            && first.TotalCapacity == second.TotalCapacity
            && first.CloseAtUtc == second.CloseAtUtc
            && first.CapacityCreatedAtUtc == second.CapacityCreatedAtUtc
            && first.CapacityCreatedBy == second.CapacityCreatedBy
            && first.MovementId == second.MovementId
            && first.MovementType == second.MovementType
            && first.MovementQuantity == second.MovementQuantity
            && first.MovementAvailableDelta == second.MovementAvailableDelta
            && first.MovementResultingRemaining == second.MovementResultingRemaining
            && first.MovementResultingVersion == second.MovementResultingVersion
            && first.MovementReason == second.MovementReason
            && first.MovementReference == second.MovementReference
            && first.MovementActor == second.MovementActor
            && first.MovementOccurredAtUtc == second.MovementOccurredAtUtc;
    }

    public bool PreOrderCapacityCurrentStateExactlyMatches(ProductMutationEvidence other)
    {
        if (!HasPreOrderCapacityCreation || !other.HasPreOrderCapacityCreation) return false;
        var first = PreOrderCapacityEvidence;
        var second = other.PreOrderCapacityEvidence;
        return first.HeldQuantity == second.HeldQuantity
            && first.CommittedQuantity == second.CommittedQuantity
            && first.RetiredQuantity == second.RetiredQuantity
            && first.CapacityVersion == second.CapacityVersion
            && first.CapacityUpdatedAtUtc == second.CapacityUpdatedAtUtc
            && first.CapacityUpdatedBy == second.CapacityUpdatedBy;
    }

    private static DateTimeOffset Normalize(DateTimeOffset value)
    {
        const long ticksPerMicrosecond = TimeSpan.TicksPerMillisecond / 1000;
        return new DateTimeOffset(
            value.Ticks - (value.Ticks % ticksPerMicrosecond),
            TimeSpan.Zero);
    }

    private static DateTimeOffset? NormalizeNullable(DateTimeOffset? value) =>
        value.HasValue ? Normalize(value.Value) : null;
}
