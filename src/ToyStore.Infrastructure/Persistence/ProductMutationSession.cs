using System.Runtime.ExceptionServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToyStore.Application.Common.Models;
using ToyStore.Application.Common.Persistence;
using ToyStore.Application.Products;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence;

internal sealed class ProductMutationSessionFactory(
    IDbContextFactory<ApplicationDbContext> contextFactory)
    : IProductMutationSessionFactory
{
    public async ValueTask<IProductMutationSession> OpenAsync(
        CancellationToken cancellationToken) =>
        new ProductMutationSession(
            await contextFactory.CreateDbContextAsync(cancellationToken));

    public async Task<CatalogCommitVerification<ProductMutationEvidence>> VerifyCommitAsync(
        ProductMutationEvidence evidence,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        try
        {
            await using var db = await contextFactory.CreateDbContextAsync(cancellationToken);
            var product = await db.Products
                .AsNoTracking()
                .Include(current => current.Images)
                .Include(current => current.Characters)
                .AsSingleQuery()
                .SingleOrDefaultAsync(current => current.Id == evidence.Id, cancellationToken);
            InventoryItem? inventory = null;
            StockMovement? initialMovement = null;
            PreOrderCapacity? preOrderCapacity = null;
            PreOrderCapacityMovement? initialCapacityMovement = null;
            if (evidence.HasInventoryCreation)
            {
                var inventoryEvidence = evidence.InventoryEvidence;
                inventory = await db.InventoryItems.AsNoTracking().SingleOrDefaultAsync(
                    current => current.Id == inventoryEvidence.InventoryItemId,
                    cancellationToken);
                initialMovement = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(
                    current => current.Id == inventoryEvidence.OperationId,
                    cancellationToken);
            }

            if (evidence.HasPreOrderCapacityCreation)
            {
                var capacityEvidence = evidence.PreOrderCapacityEvidence;
                preOrderCapacity = await db.PreOrderCapacities.AsNoTracking().SingleOrDefaultAsync(
                    current => current.Id == capacityEvidence.CapacityId,
                    cancellationToken);
                initialCapacityMovement = await db.PreOrderCapacityMovements.AsNoTracking().SingleOrDefaultAsync(
                    current => current.Id == capacityEvidence.MovementId,
                    cancellationToken);
            }

            if (product is null)
            {
                return inventory is null && initialMovement is null
                    && preOrderCapacity is null && initialCapacityMovement is null
                    ? CatalogCommitVerificationResult.NotCommitted<ProductMutationEvidence>()
                    : CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            if (evidence.HasInventoryCreation && (inventory is null || initialMovement is null))
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }


            if (evidence.HasPreOrderCapacityCreation
                && (preOrderCapacity is null || initialCapacityMovement is null))
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            if (evidence.HasInventoryCreation
                && (inventory!.ProductId != evidence.Id
                    || initialMovement!.InventoryItemId != inventory.Id
                    || initialMovement.ProductId != evidence.Id
                    || initialMovement.Type != StockMovementType.InitialStock))
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            var authoritative = evidence.HasInventoryCreation
                ? ProductMutationEvidence.Capture(product, inventory!, initialMovement!)
                : evidence.HasPreOrderCapacityCreation
                    ? ProductMutationEvidence.Capture(product, preOrderCapacity!, initialCapacityMovement!)
                    : ProductMutationEvidence.Capture(product);
            if (product.Version < evidence.IntendedVersion)
            {
                return evidence.HasInventoryCreation
                    ? CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>()
                    : CatalogCommitVerificationResult.NotCommitted<ProductMutationEvidence>();
            }

            if (!evidence.HasInventoryCreation)
            {
                if (evidence.HasPreOrderCapacityCreation)
                {
                    if (!evidence.PreOrderCapacityCreationExactlyMatches(authoritative)
                        || preOrderCapacity!.Version < evidence.PreOrderCapacityEvidence.CapacityVersion)
                    {
                        return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
                    }

                    if (product.Version == evidence.IntendedVersion
                        && !evidence.ProductSnapshotExactlyMatches(authoritative))
                    {
                        return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
                    }

                    if (preOrderCapacity.Version == evidence.PreOrderCapacityEvidence.CapacityVersion
                        && !evidence.PreOrderCapacityCurrentStateExactlyMatches(authoritative))
                    {
                        return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
                    }

                    return product.Version > evidence.IntendedVersion
                        || preOrderCapacity.Version > evidence.PreOrderCapacityEvidence.CapacityVersion
                        ? CatalogCommitVerificationResult.Superseded(authoritative)
                        : CatalogCommitVerificationResult.Committed(authoritative);
                }

                if (product.Version > evidence.IntendedVersion)
                {
                    return CatalogCommitVerificationResult.Superseded(authoritative);
                }

                return evidence.ProductSnapshotExactlyMatches(authoritative)
                    ? CatalogCommitVerificationResult.Committed(authoritative)
                    : CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            if (!evidence.InventoryCreationProofExactlyMatches(authoritative)
                || inventory!.Version < evidence.InventoryEvidence.IntendedVersion)
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            if (product.Version == evidence.IntendedVersion
                && !evidence.ProductSnapshotExactlyMatches(authoritative))
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            if (inventory.Version == evidence.InventoryEvidence.IntendedVersion
                && !evidence.InventoryCurrentStateExactlyMatches(authoritative))
            {
                return CatalogCommitVerificationResult.Inconsistent<ProductMutationEvidence>();
            }

            return product.Version > evidence.IntendedVersion
                || inventory.Version > evidence.InventoryEvidence.IntendedVersion
                ? CatalogCommitVerificationResult.Superseded(authoritative)
                : CatalogCommitVerificationResult.Committed(authoritative);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            return CatalogCommitVerificationResult.Unavailable<ProductMutationEvidence>();
        }
    }
}

internal sealed class ProductMutationSession(ApplicationDbContext db) : IProductMutationSession
{
    private IDbContextTransaction? transaction;
    private int executionStarted;
    private bool operationActive;
    private bool namespaceLocked;
    private bool productLockAttempted;
    private Guid lockedProductId;
    private bool capacityLockAttempted;
    private Guid lockedCapacityId;
    private bool referencesLockAttempted;
    private bool referencesLocked;
    private bool resourcesDisposed;

    public async Task AcquireNamespaceLockAsync(CancellationToken cancellationToken)
    {
        EnsureActive();
        if (!namespaceLocked)
        {
            await new CatalogReferenceMutationLock(db).AcquireProductAsync(cancellationToken);
            namespaceLocked = true;
        }
    }

    public async Task<Product?> LockProductAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        EnsureNamespaceLocked();
        if (productLockAttempted)
        {
            throw new InvalidOperationException("A Product session can lock one Product only.");
        }

        productLockAttempted = true;
        lockedProductId = productId;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE",
            cancellationToken);
        return await db.Products
            .Include(product => product.Images)
            .Include(product => product.Characters)
            .AsSingleQuery()
            .SingleOrDefaultAsync(product => product.Id == productId, cancellationToken);
    }

    public async Task<PreOrderCapacity?> LockPreOrderCapacityAsync(
        Guid productId,
        CancellationToken cancellationToken)
    {
        EnsureProductLockAttempted();
        if (productId != lockedProductId)
        {
            throw new InvalidOperationException(
                "Pre-order capacity must belong to the locked Product.");
        }

        if (capacityLockAttempted)
        {
            throw new InvalidOperationException(
                "A Product session can lock one Pre-order capacity only.");
        }

        capacityLockAttempted = true;
        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"PreOrderCapacities\" WHERE \"ProductId\" = {productId} FOR UPDATE",
            cancellationToken);
        var capacity = await db.PreOrderCapacities.SingleOrDefaultAsync(
            current => current.ProductId == productId,
            cancellationToken);
        lockedCapacityId = capacity?.Id ?? Guid.Empty;
        return capacity;
    }

    public async Task<ProductReferenceReadiness> LockReferencesAsync(
        Guid productCategoryId,
        Guid brandId,
        Guid universeId,
        IReadOnlyCollection<Guid> characterIds,
        CancellationToken cancellationToken)
    {
        EnsureProductLockAttempted();
        ArgumentNullException.ThrowIfNull(characterIds);
        if (referencesLockAttempted)
        {
            throw new InvalidOperationException(
                "A Product session can lock one reference set only.");
        }

        referencesLockAttempted = true;
        var characterSnapshot = characterIds.ToArray();
        var distinctCharacterIds = characterSnapshot.Distinct().Order().ToArray();

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Brands\" WHERE \"Id\" = {brandId} FOR UPDATE",
            cancellationToken);
        var brand = await db.Brands.SingleOrDefaultAsync(
            current => current.Id == brandId,
            cancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync(
            $"SELECT 1 FROM \"Universes\" WHERE \"Id\" = {universeId} FOR UPDATE",
            cancellationToken);
        var universe = await db.Universes.SingleOrDefaultAsync(
            current => current.Id == universeId,
            cancellationToken);

        var categoryAllowed = (productCategoryId == CatalogSeedIds.ArtToyCategory
                || productCategoryId == CatalogSeedIds.GundamCategory)
            && await db.ProductCategories.AnyAsync(
                category => category.Id == productCategoryId,
                cancellationToken);
        var existingCharacters = distinctCharacterIds.Length == 0
            ? []
            : await db.Characters
                .Where(character => character.UniverseId == universeId
                    && distinctCharacterIds.Contains(character.Id))
                .Select(character => character.Id)
                .Order()
                .ToArrayAsync(cancellationToken);
        referencesLocked = true;
        return new ProductReferenceReadiness(
            categoryAllowed,
            brand is not null,
            brand?.Status,
            brand?.Image is not null,
            universe is not null,
            universe?.Status,
            universe?.Logo is not null,
            distinctCharacterIds.Length == characterSnapshot.Length,
            existingCharacters);
    }

    public Task<bool> DisplayNameExistsAsync(
        string normalizedDisplayName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureReferencesLocked();
        return db.Products.AnyAsync(product =>
            product.NormalizedDisplayName == normalizedDisplayName
            && (!excludedId.HasValue || product.Id != excludedId.Value), cancellationToken);
    }

    public Task<bool> EnglishNameExistsAsync(
        string normalizedEnglishName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureReferencesLocked();
        return db.Products.AnyAsync(product =>
            product.NormalizedEnglishName == normalizedEnglishName
            && (!excludedId.HasValue || product.Id != excludedId.Value), cancellationToken);
    }

    public Task<CatalogSlug> AllocateSlugAsync(
        string englishName,
        Guid? excludedId,
        CancellationToken cancellationToken)
    {
        EnsureReferencesLocked();
        return new CatalogSlugAllocator(db).AllocateProductForLockedMutationAsync(
            englishName,
            excludedId,
            cancellationToken);
    }

    public void Add(Product product, InventoryCreation inventoryCreation)
    {
        EnsureReferencesLocked();

        ArgumentNullException.ThrowIfNull(product);
        ArgumentNullException.ThrowIfNull(inventoryCreation);
        if (product.Id != lockedProductId)
        {
            throw new InvalidOperationException(
                "Product does not match the row-lock attempt for this session.");
        }

        if (inventoryCreation.Item.ProductId != product.Id
            || inventoryCreation.InitialMovement.ProductId != product.Id)
        {
            throw new InvalidOperationException("Inventory creation does not belong to Product.");
        }

        db.Products.Add(product);
        db.InventoryItems.Add(inventoryCreation.Item);
        db.StockMovements.Add(inventoryCreation.InitialMovement);
    }

    public void Add(Product product)
    {
        EnsureReferencesLocked();
        ArgumentNullException.ThrowIfNull(product);
        if (product.Id != lockedProductId)
        {
            throw new InvalidOperationException("Product does not match the row-lock attempt for this session.");
        }

        db.Products.Add(product);
    }

    public void Add(PreOrderCapacityCreation capacityCreation)
    {
        EnsureReferencesLocked();
        ArgumentNullException.ThrowIfNull(capacityCreation);
        if (capacityCreation.Capacity.ProductId != lockedProductId
            || capacityCreation.Movement.ProductId != lockedProductId
            || capacityCreation.Movement.CapacityId != capacityCreation.Capacity.Id)
        {
            throw new InvalidOperationException("Pre-order capacity creation does not belong to Product.");
        }

        db.PreOrderCapacities.Add(capacityCreation.Capacity);
        db.PreOrderCapacityMovements.Add(capacityCreation.Movement);
    }

    public void Add(PreOrderCapacityMovement movement)
    {
        EnsureReferencesLocked();
        ArgumentNullException.ThrowIfNull(movement);
        if (!capacityLockAttempted
            || lockedCapacityId == Guid.Empty
            || movement.CapacityId != lockedCapacityId
            || movement.ProductId != lockedProductId)
        {
            throw new InvalidOperationException(
                "Pre-order capacity movement does not belong to the locked Product capacity.");
        }

        db.PreOrderCapacityMovements.Add(movement);
    }

    public async Task<CatalogMutationExecution<T>> ExecuteOnceAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (Interlocked.Exchange(ref executionStarted, 1) != 0)
        {
            throw new InvalidOperationException("The Product mutation session can execute only once.");
        }

        CatalogMutationExecution<T>? execution = null;
        Exception? executionException = null;
        IReadOnlyList<Exception> cleanupExceptions;
        try
        {
            transaction = await db.Database.BeginTransactionAsync(cancellationToken);
            operationActive = true;
            execution = await ExecuteOperationAsync(operation, cancellationToken);
        }
        catch (Exception exception)
        {
            executionException = exception;
        }
        finally
        {
            operationActive = false;
            cleanupExceptions = await ReleaseResourcesAsync();
        }

        if (executionException is not null)
        {
            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException(
                    "Product mutation and persistence cleanup failed.",
                    new[] { executionException }.Concat(cleanupExceptions));
            }

            ExceptionDispatchInfo.Capture(executionException).Throw();
        }

        return AttachCleanupFailures(execution!, cleanupExceptions);
    }

    public async ValueTask DisposeAsync()
    {
        var failures = await ReleaseResourcesAsync();
        if (failures.Count == 1)
        {
            ExceptionDispatchInfo.Capture(failures[0]).Throw();
        }

        if (failures.Count > 1)
        {
            throw new AggregateException("Product persistence cleanup failed.", failures);
        }
    }

    private async Task<CatalogMutationExecution<T>> ExecuteOperationAsync<T>(
        Func<CancellationToken, Task<Result<T>>> operation,
        CancellationToken cancellationToken)
    {
        var rollbackAttempted = false;
        try
        {
            var result = await operation(cancellationToken);
            if (result.IsFailure)
            {
                rollbackAttempted = true;
                await RollbackAndClearAsync();
                return new CatalogMutationExecution<T>(
                    result,
                    CatalogCommitOutcome.DefinitelyRolledBack);
            }

            await db.SaveChangesAsync(cancellationToken);
            try
            {
                await transaction!.CommitAsync(cancellationToken);
                return new CatalogMutationExecution<T>(result, CatalogCommitOutcome.Committed);
            }
            catch (Exception exception)
            {
                db.ChangeTracker.Clear();
                return new CatalogMutationExecution<T>(
                    result,
                    CatalogCommitOutcome.Indeterminate,
                    CatalogCommitFailure.Create(exception));
            }
        }
        catch (Exception exception)
        {
            if (!rollbackAttempted)
            {
                await RollbackAndRethrowAsync(exception);
            }

            ExceptionDispatchInfo.Capture(exception).Throw();
            throw;
        }
    }

    private async Task RollbackAndClearAsync()
    {
        try
        {
            await transaction!.RollbackAsync(CancellationToken.None);
        }
        finally
        {
            db.ChangeTracker.Clear();
        }
    }

    private async Task RollbackAndRethrowAsync(Exception original)
    {
        try
        {
            await RollbackAndClearAsync();
        }
        catch (Exception rollback)
        {
            throw new AggregateException(
                "Product mutation and rollback failed.",
                original,
                rollback);
        }
    }

    private async Task<IReadOnlyList<Exception>> ReleaseResourcesAsync()
    {
        if (resourcesDisposed)
        {
            return [];
        }

        resourcesDisposed = true;
        var failures = new List<Exception>();
        try
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
        catch (Exception exception)
        {
            failures.Add(exception);
        }
        finally
        {
            transaction = null;
            try
            {
                await db.DisposeAsync();
            }
            catch (Exception exception)
            {
                failures.Add(exception);
            }
        }

        return failures;
    }

    private static CatalogMutationExecution<T> AttachCleanupFailures<T>(
        CatalogMutationExecution<T> execution,
        IReadOnlyList<Exception> failures)
    {
        if (failures.Count == 0)
        {
            return execution;
        }

        var names = failures.Select(failure =>
            failure.GetType().FullName ?? failure.GetType().Name).ToArray();
        return execution.CommitFailure is null
            ? execution with { CleanupFailureTypes = names }
            : execution with
            {
                CommitFailure = execution.CommitFailure with
                {
                    CleanupFailureTypes = execution.CommitFailure.CleanupFailureTypes
                        .Concat(names)
                        .ToArray(),
                },
            };
    }

    private void EnsureActive()
    {
        if (!operationActive)
        {
            throw new InvalidOperationException(
                "Product persistence operations require ExecuteOnceAsync.");
        }
    }

    private void EnsureNamespaceLocked()
    {
        EnsureActive();
        if (!namespaceLocked)
        {
            throw new InvalidOperationException("Product namespace must be locked first.");
        }
    }

    private void EnsureProductLockAttempted()
    {
        EnsureNamespaceLocked();
        if (!productLockAttempted)
        {
            throw new InvalidOperationException("Product row lock must be attempted first.");
        }
    }

    private void EnsureReferencesLocked()
    {
        EnsureProductLockAttempted();
        if (!referencesLocked)
        {
            throw new InvalidOperationException(
                "Product references must be locked before persistence checks.");
        }
    }
}
