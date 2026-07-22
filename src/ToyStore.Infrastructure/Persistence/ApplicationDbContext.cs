using System.Runtime.ExceptionServices;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ToyStore.Application.Common.Interfaces;
using ToyStore.Domain.Addresses;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Notifications;
using ToyStore.Domain.Orders;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.Infrastructure.Persistence;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options), IApplicationDbContext
{
    public DbSet<Product> Products => Set<Product>();

    public DbSet<ProductImage> ProductImages => Set<ProductImage>();

    public DbSet<ProductCharacter> ProductCharacters => Set<ProductCharacter>();

    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();

    public DbSet<Brand> Brands => Set<Brand>();

    public DbSet<Universe> Universes => Set<Universe>();

    public DbSet<Character> Characters => Set<Character>();

    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();

    public DbSet<StockMovement> StockMovements => Set<StockMovement>();

    public DbSet<StockReservation> StockReservations => Set<StockReservation>();

    public DbSet<PreOrderCapacity> PreOrderCapacities => Set<PreOrderCapacity>();

    public DbSet<PreOrderCapacityReservation> PreOrderCapacityReservations =>
        Set<PreOrderCapacityReservation>();

    public DbSet<PreOrderCapacityMovement> PreOrderCapacityMovements =>
        Set<PreOrderCapacityMovement>();

    public DbSet<Cart> Carts => Set<Cart>();

    public DbSet<CartItem> CartItems => Set<CartItem>();

    public DbSet<CartOperation> CartOperations => Set<CartOperation>();

    public DbSet<CheckoutAttempt> CheckoutAttempts => Set<CheckoutAttempt>();

    public DbSet<Order> Orders => Set<Order>();

    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<Shipment> Shipments => Set<Shipment>();
    public DbSet<OrderAuditEvent> OrderAuditEvents => Set<OrderAuditEvent>();

    public DbSet<NotificationDelivery> NotificationDeliveries => Set<NotificationDelivery>();

    public DbSet<SavedAddress> SavedAddresses => Set<SavedAddress>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<ApplicationUser>().Property(user => user.PhoneNumber).HasMaxLength(256);
        builder.Entity<IdentityUserLogin<string>>().Property(login => login.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserLogin<string>>().Property(login => login.ProviderKey).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(token => token.LoginProvider).HasMaxLength(128);
        builder.Entity<IdentityUserToken<string>>().Property(token => token.Name).HasMaxLength(128);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (GetProductsWithChangedImageOrder().Length > 0)
        {
            throw new InvalidOperationException(
                "Product image order changes require SaveChangesAsync so the catalog image transaction can be applied safely.");
        }

        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess,
        CancellationToken cancellationToken = default)
    {
        var affectedProductIds = GetProductsWithChangedImageOrder();

        if (affectedProductIds.Length == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        var imageEntries = ChangeTracker.Entries<ProductImage>()
            .Where(entry => affectedProductIds.Contains(GetProductId(entry)))
            .ToArray();
        var stateSnapshots = imageEntries.Select(ImageEntryStateSnapshot.Capture).ToArray();
        var currentTransaction = Database.CurrentTransaction;
        var ownsTransaction = currentTransaction is null;
        var transaction = currentTransaction
            ?? await Database.BeginTransactionAsync(cancellationToken);
        const string savepointName = "CatalogImageRebuild";
        var savepointCreated = false;
        var transactionCompleted = false;
        int rows;

        try
        {
            if (!ownsTransaction)
            {
                await transaction.CreateSavepointAsync(savepointName, cancellationToken);
                savepointCreated = true;
            }

            foreach (var productId in affectedProductIds)
            {
                await Database.ExecuteSqlInterpolatedAsync(
                    $"SELECT 1 FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE",
                    cancellationToken);
            }

            await EnsureCompleteImageAggregatesAsync(
                affectedProductIds,
                imageEntries,
                cancellationToken);

            foreach (var productId in affectedProductIds)
            {
                await Database.ExecuteSqlInterpolatedAsync(
                    $"DELETE FROM \"ProductImages\" WHERE \"ProductId\" = {productId}",
                    cancellationToken);
            }

            foreach (var entry in imageEntries)
            {
                entry.State = entry.State == EntityState.Deleted
                    ? EntityState.Detached
                    : EntityState.Added;
            }

            rows = await base.SaveChangesAsync(
                acceptAllChangesOnSuccess: false,
                cancellationToken);

            if (!acceptAllChangesOnSuccess)
            {
                RestoreImageEntryStates(stateSnapshots);
            }

            if (ownsTransaction)
            {
                await transaction.CommitAsync(cancellationToken);
                transactionCompleted = true;
            }
            else
            {
                await transaction.ReleaseSavepointAsync(savepointName, cancellationToken);
                savepointCreated = false;
                transactionCompleted = true;
            }
        }
        catch (Exception originalException)
        {
            var cleanupExceptions = new List<Exception>();
            try
            {
                if (!transactionCompleted && ownsTransaction)
                {
                    await transaction.RollbackAsync(CancellationToken.None);
                }
                else if (!transactionCompleted && savepointCreated)
                {
                    await transaction.RollbackToSavepointAsync(
                        savepointName,
                        CancellationToken.None);
                }
            }
            catch (Exception rollbackException)
            {
                cleanupExceptions.Add(rollbackException);
            }

            foreach (var snapshot in stateSnapshots)
            {
                try
                {
                    snapshot.Restore(this);
                }
                catch (Exception restoreException)
                {
                    cleanupExceptions.Add(restoreException);
                }
            }

            if (cleanupExceptions.Count > 0)
            {
                throw new AggregateException(
                    "Catalog image persistence failed and one or more cleanup operations also failed.",
                    new[] { originalException }.Concat(cleanupExceptions));
            }

            ExceptionDispatchInfo.Capture(originalException).Throw();
            throw;
        }
        finally
        {
            if (ownsTransaction)
            {
                await transaction.DisposeAsync();
            }
        }

        if (acceptAllChangesOnSuccess)
        {
            ChangeTracker.AcceptAllChanges();
        }

        return rows;
    }

    private Guid[] GetProductsWithChangedImageOrder()
    {
        ChangeTracker.DetectChanges();
        return ChangeTracker.Entries<ProductImage>()
            .Where(entry => entry.State == EntityState.Modified
                && (entry.Property(nameof(ProductImage.SortOrder)).IsModified
                    || entry.Property(nameof(ProductImage.IsPrimary)).IsModified))
            .Select(GetProductId)
            .Distinct()
            .Order()
            .ToArray();
    }

    private void RestoreImageEntryStates(
        IEnumerable<ImageEntryStateSnapshot> stateSnapshots)
    {
        foreach (var snapshot in stateSnapshots)
        {
            snapshot.Restore(this);
        }
    }

    private async Task EnsureCompleteImageAggregatesAsync(
        IReadOnlyCollection<Guid> productIds,
        IReadOnlyCollection<Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ProductImage>> imageEntries,
        CancellationToken cancellationToken)
    {
        foreach (var productId in productIds)
        {
            var productEntry = ChangeTracker.Entries<Product>()
                .SingleOrDefault(entry => entry.Entity.Id == productId);
            if (productEntry is null
                || !productEntry.Collection(product => product.Images).IsLoaded)
            {
                throw new InvalidOperationException(
                    "Product images must be fully loaded before their order can be persisted.");
            }

            var databaseImageIds = await ProductImages
                .Where(image => EF.Property<Guid>(image, "ProductId") == productId)
                .Select(image => image.Id)
                .ToArrayAsync(cancellationToken);
            var trackedPersistedImageIds = imageEntries
                .Where(entry => GetProductId(entry) == productId
                    && entry.State is not EntityState.Added and not EntityState.Detached)
                .Select(entry => entry.Entity.Id)
                .Where(databaseImageIds.Contains)
                .ToHashSet();
            if (!trackedPersistedImageIds.SetEquals(databaseImageIds))
            {
                throw new InvalidOperationException(
                    "Product image persistence refused a partial aggregate snapshot.");
            }
        }
    }

    private static Guid GetProductId(
        Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ProductImage> entry)
    {
        var property = entry.Property<Guid>("ProductId");
        return property.CurrentValue != Guid.Empty
            ? property.CurrentValue
            : property.OriginalValue;
    }

    private sealed record ImageEntryStateSnapshot(
        ProductImage Entity,
        EntityState State,
        IReadOnlyDictionary<string, ImagePropertyState> Properties)
    {
        internal static ImageEntryStateSnapshot Capture(
            Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<ProductImage> entry) =>
            new(
                entry.Entity,
                entry.State,
                entry.Properties.ToDictionary(
                    property => property.Metadata.Name,
                    property => new ImagePropertyState(
                        property.CurrentValue,
                        property.OriginalValue,
                        property.IsModified),
                    StringComparer.Ordinal));

        internal void Restore(ApplicationDbContext context)
        {
            var entry = context.Entry(Entity);
            entry.State = EntityState.Unchanged;
            foreach (var property in entry.Properties)
            {
                if (Properties.TryGetValue(property.Metadata.Name, out var snapshot))
                {
                    property.CurrentValue = snapshot.CurrentValue;
                    property.OriginalValue = snapshot.OriginalValue;
                }
            }

            entry.State = State;
            foreach (var property in entry.Properties)
            {
                if (Properties.TryGetValue(property.Metadata.Name, out var snapshot))
                {
                    property.IsModified = snapshot.IsModified;
                }
            }
        }
    }

    private sealed record ImagePropertyState(
        object? CurrentValue,
        object? OriginalValue,
        bool IsModified);

    public async Task<T> ExecuteInTransactionAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(operation);

        var strategy = Database.CreateExecutionStrategy();

        return await strategy.ExecuteAsync(async retryCancellationToken =>
        {
            await using var transaction =
                await Database.BeginTransactionAsync(retryCancellationToken);
            var result = await operation(retryCancellationToken);

            await SaveChangesAsync(retryCancellationToken);
            await transaction.CommitAsync(retryCancellationToken);

            return result;
        }, cancellationToken);
    }
}
