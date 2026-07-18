using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;
using ToyStore.Infrastructure.Persistence;

namespace ToyStore.UnitTests.Infrastructure;

public sealed class InventoryModelConfigurationTests
{
    private static readonly IModel Model = CreateModel();

    [Fact]
    public void DbContextMapsOnlyTheApprovedInventoryEntitiesWithoutProductStock()
    {
        Assert.Equal(typeof(DbSet<InventoryItem>), DbSetProperty(nameof(ApplicationDbContext.InventoryItems)).PropertyType);
        Assert.Equal(typeof(DbSet<StockMovement>), DbSetProperty(nameof(ApplicationDbContext.StockMovements)).PropertyType);
        Assert.Equal(typeof(DbSet<StockReservation>), DbSetProperty(nameof(ApplicationDbContext.StockReservations)).PropertyType);

        Assert.Equal("InventoryItems", Entity<InventoryItem>().GetTableName());
        Assert.Equal("StockMovements", Entity<StockMovement>().GetTableName());
        Assert.Equal("StockReservations", Entity<StockReservation>().GetTableName());
        Assert.DoesNotContain(
            Entity<Product>().GetProperties(),
            property => property.Name.Contains("Stock", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Inventory", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void InventoryItemHasOneProductCompositeOwnershipAuditAndConcurrencyGuards()
    {
        var item = Entity<InventoryItem>();

        Assert.Equal([nameof(InventoryItem.Id)], item.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(item.GetKeys(), key =>
            key.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(InventoryItem.Id), nameof(InventoryItem.ProductId)]));
        Assert.Contains(item.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(InventoryItem.ProductId)]));
        var productForeignKey = Assert.Single(item.GetForeignKeys());
        Assert.Equal(typeof(Product), productForeignKey.PrincipalEntityType.ClrType);
        Assert.Equal(DeleteBehavior.Restrict, productForeignKey.DeleteBehavior);

        AssertProperty(item, nameof(InventoryItem.CreatedAtUtc), "timestamp with time zone");
        AssertProperty(item, nameof(InventoryItem.UpdatedAtUtc), "timestamp with time zone");
        AssertLength(item, nameof(InventoryItem.CreatedBy), InventoryLimits.ActorLength);
        AssertLength(item, nameof(InventoryItem.UpdatedBy), InventoryLimits.ActorLength);
        var version = item.FindProperty(nameof(InventoryItem.Version))!;
        Assert.Equal("bigint", version.GetColumnType());
        Assert.True(version.IsConcurrencyToken);
        Assert.False(version.IsNullable);

        AssertChecks(
            item,
            "CK_InventoryItems_OnHandQuantity_NonNegative",
            "CK_InventoryItems_HeldQuantity_Bounds",
            "CK_InventoryItems_Version_Positive",
            "CK_InventoryItems_Audit_Chronology",
            "CK_InventoryItems_Audit_Actors_NotBlank");
    }

    [Fact]
    public void MovementHasImmutableEvidenceShapeUniquenessHistoryIndexAndCompositeOwnership()
    {
        var movement = Entity<StockMovement>();

        Assert.Equal([nameof(StockMovement.Id)], movement.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Equal("character varying(32)", movement.FindProperty(nameof(StockMovement.Type))!.GetColumnType());
        AssertLength(movement, nameof(StockMovement.Reason), InventoryLimits.ReasonLength);
        AssertLength(movement, nameof(StockMovement.Reference), InventoryLimits.ReferenceLength);
        AssertLength(movement, nameof(StockMovement.Actor), InventoryLimits.ActorLength);
        AssertProperty(movement, nameof(StockMovement.OccurredAtUtc), "timestamp with time zone");

        Assert.Contains(movement.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.InventoryItemId), nameof(StockMovement.ResultingInventoryVersion)]));
        var initial = Assert.Single(movement.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.InventoryItemId)]));
        Assert.Equal("\"Type\" = 'InitialStock'", initial.GetFilter());
        Assert.Contains(movement.GetIndexes(), index =>
            !index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.InventoryItemId), nameof(StockMovement.OccurredAtUtc), nameof(StockMovement.Id)]));
        var consumeReservation = Assert.Single(movement.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.ReservationId)]));
        Assert.Equal("\"ReservationId\" IS NOT NULL", consumeReservation.GetFilter());

        Assert.Contains(movement.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(InventoryItem)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.InventoryItemId), nameof(StockMovement.ProductId)])
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(movement.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(StockReservation)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.ReservationId), nameof(StockMovement.InventoryItemId), nameof(StockMovement.ProductId)])
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);

        AssertChecks(
            movement,
            "CK_StockMovements_Quantity_Evidence",
            "CK_StockMovements_Version_MatchesType",
            "CK_StockMovements_ResultingOnHandQuantity_NonNegative",
            "CK_StockMovements_ResultingInventoryVersion_Positive",
            "CK_StockMovements_Evidence_NotBlank");
    }

    [Fact]
    public void ReservationHasCompositeOwnershipLifecycleChecksAndNullableReverseMovementForeignKey()
    {
        var reservation = Entity<StockReservation>();

        Assert.Equal([nameof(StockReservation.Id)], reservation.FindPrimaryKey()!.Properties.Select(property => property.Name));
        Assert.Contains(reservation.GetKeys(), key =>
            key.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.Id), nameof(StockReservation.InventoryItemId), nameof(StockReservation.ProductId)]));
        Assert.Equal("character varying(32)", reservation.FindProperty(nameof(StockReservation.Status))!.GetColumnType());
        AssertProperty(reservation, nameof(StockReservation.ReservedAtUtc), "timestamp with time zone");
        AssertProperty(reservation, nameof(StockReservation.ExpiresAtUtc), "timestamp with time zone");
        AssertProperty(reservation, nameof(StockReservation.TerminalAtUtc), "timestamp with time zone");
        AssertLength(reservation, nameof(StockReservation.ReserveReason), InventoryLimits.ReasonLength);
        AssertLength(reservation, nameof(StockReservation.ReserveReference), InventoryLimits.ReferenceLength);
        AssertLength(reservation, nameof(StockReservation.ReservedBy), InventoryLimits.ActorLength);
        AssertOptionalLength(reservation, nameof(StockReservation.TerminalReason), InventoryLimits.ReasonLength);
        AssertOptionalLength(reservation, nameof(StockReservation.TerminalReference), InventoryLimits.ReferenceLength);
        AssertOptionalLength(reservation, nameof(StockReservation.TerminalActor), InventoryLimits.ActorLength);

        Assert.Contains(reservation.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.CheckoutAttemptId)]));
        Assert.Contains(reservation.GetIndexes(), index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.InventoryItemId), nameof(StockReservation.Status), nameof(StockReservation.ExpiresAtUtc)]));
        var consumedMovement = Assert.Single(reservation.GetIndexes(), index =>
            index.IsUnique
            && index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.ConsumedMovementId)]));
        Assert.Equal("\"ConsumedMovementId\" IS NOT NULL", consumedMovement.GetFilter());

        Assert.Contains(reservation.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(InventoryItem)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.InventoryItemId), nameof(StockReservation.ProductId)])
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);
        Assert.Contains(reservation.GetForeignKeys(), foreignKey =>
            foreignKey.PrincipalEntityType.ClrType == typeof(StockMovement)
            && foreignKey.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockReservation.ConsumedMovementId)])
            && foreignKey.PrincipalKey.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(StockMovement.Id)])
            && foreignKey.DeleteBehavior == DeleteBehavior.Restrict);

        AssertChecks(
            reservation,
            "CK_StockReservations_Quantity_Positive",
            "CK_StockReservations_Expiry_AfterReserved",
            "CK_StockReservations_Lifecycle_Evidence",
            "CK_StockReservations_Terminal_Chronology",
            "CK_StockReservations_Evidence_NotBlank");
    }

    private static System.Reflection.PropertyInfo DbSetProperty(string name) =>
        typeof(ApplicationDbContext).GetProperty(name)
        ?? throw new InvalidOperationException($"Missing DbSet {name}.");

    private static IEntityType Entity<TEntity>() =>
        Model.FindEntityType(typeof(TEntity))
        ?? throw new InvalidOperationException($"Missing EF entity {typeof(TEntity).Name}.");

    private static void AssertProperty(IEntityType entity, string name, string columnType)
    {
        var property = entity.FindProperty(name)!;
        Assert.Equal(columnType, property.GetColumnType());
    }

    private static void AssertLength(IEntityType entity, string name, int maxLength)
    {
        var property = entity.FindProperty(name)!;
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.False(property.IsNullable);
    }

    private static void AssertOptionalLength(IEntityType entity, string name, int maxLength)
    {
        var property = entity.FindProperty(name)!;
        Assert.Equal(maxLength, property.GetMaxLength());
        Assert.True(property.IsNullable);
    }

    private static void AssertChecks(IEntityType entity, params string[] expectedNames)
    {
        var actual = entity.GetCheckConstraints().Select(check => check.Name).ToHashSet();
        Assert.All(expectedNames, name => Assert.Contains(name, actual));
    }

    private static IModel CreateModel()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Database=toystore_model_test;Username=test;Password=test",
                npgsql => npgsql.MigrationsAssembly(typeof(ApplicationDbContext).Assembly.FullName))
            .Options;
        using var context = new ApplicationDbContext(options);
        return context.GetService<IDesignTimeModel>().Model;
    }
}
