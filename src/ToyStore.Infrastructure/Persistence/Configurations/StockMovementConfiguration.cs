using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Inventory;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class StockMovementConfiguration : IEntityTypeConfiguration<StockMovement>
{
    public void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        builder.ToTable("StockMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_StockMovements_Quantity_Evidence",
                "(\"Type\" = 'InitialStock' AND \"QuantityDelta\" >= 0 AND \"ReservationId\" IS NULL AND \"ResultingInventoryVersion\" = 1 AND \"QuantityDelta\" = \"ResultingOnHandQuantity\") OR "
                + "(\"Type\" = 'Received' AND \"QuantityDelta\" > 0 AND \"ReservationId\" IS NULL) OR "
                + "(\"Type\" = 'Adjusted' AND \"QuantityDelta\" <> 0 AND \"ReservationId\" IS NULL) OR "
                + "(\"Type\" = 'ReservationConsumed' AND \"QuantityDelta\" < 0 AND \"ReservationId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_StockMovements_ResultingOnHandQuantity_NonNegative",
                "\"ResultingOnHandQuantity\" >= 0");
            table.HasCheckConstraint(
                "CK_StockMovements_ResultingInventoryVersion_Positive",
                "\"ResultingInventoryVersion\" > 0");
            table.HasCheckConstraint(
                "CK_StockMovements_Version_MatchesType",
                "(\"Type\" = 'InitialStock' AND \"ResultingInventoryVersion\" = 1) OR "
                + "(\"Type\" <> 'InitialStock' AND \"ResultingInventoryVersion\" > 1)");
            table.HasCheckConstraint(
                "CK_StockMovements_Evidence_NotBlank",
                "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
        });

        builder.HasKey(movement => movement.Id)
            .HasName("PK_StockMovements");
        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(movement => movement.Reason)
            .HasMaxLength(InventoryLimits.ReasonLength)
            .IsRequired();
        builder.Property(movement => movement.Reference)
            .HasMaxLength(InventoryLimits.ReferenceLength)
            .IsRequired();
        builder.Property(movement => movement.Actor)
            .HasMaxLength(InventoryLimits.ActorLength)
            .IsRequired();
        builder.Property(movement => movement.OccurredAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(movement => new
        {
            movement.InventoryItemId,
            movement.ResultingInventoryVersion,
        })
            .IsUnique()
            .HasDatabaseName("UX_StockMovements_InventoryItemId_ResultingInventoryVersion");
        builder.HasIndex(movement => movement.InventoryItemId)
            .IsUnique()
            .HasFilter("\"Type\" = 'InitialStock'")
            .HasDatabaseName("UX_StockMovements_InventoryItemId_InitialStock");
        builder.HasIndex(movement => movement.ReservationId)
            .IsUnique()
            .HasFilter("\"ReservationId\" IS NOT NULL")
            .HasDatabaseName("UX_StockMovements_ReservationId");
        builder.HasIndex(movement => new
        {
            movement.InventoryItemId,
            movement.OccurredAtUtc,
            movement.Id,
        })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_StockMovements_InventoryItemId_OccurredAtUtc_Id");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.InventoryItemId,
                movement.ProductId,
            })
            .HasPrincipalKey(item => new { item.Id, item.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockReservation>()
            .WithMany()
            .HasForeignKey(
                nameof(StockMovement.ReservationId),
                nameof(StockMovement.InventoryItemId),
                nameof(StockMovement.ProductId))
            .HasPrincipalKey(
                nameof(StockReservation.Id),
                nameof(StockReservation.InventoryItemId),
                nameof(StockReservation.ProductId))
            .OnDelete(DeleteBehavior.Restrict);
    }
}
