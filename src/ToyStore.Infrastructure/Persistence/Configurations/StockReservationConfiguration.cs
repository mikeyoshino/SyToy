using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Inventory;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class StockReservationConfiguration : IEntityTypeConfiguration<StockReservation>
{
    public void Configure(EntityTypeBuilder<StockReservation> builder)
    {
        builder.ToTable("StockReservations", table =>
        {
            table.HasCheckConstraint(
                "CK_StockReservations_Quantity_Positive",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_StockReservations_Expiry_AfterReserved",
                "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
            table.HasCheckConstraint(
                "CK_StockReservations_Lifecycle_Evidence",
                "(\"Status\" = 'Active' AND \"TerminalAtUtc\" IS NULL AND \"TerminalActor\" IS NULL AND \"TerminalReason\" IS NULL AND \"TerminalReference\" IS NULL AND \"ConsumedMovementId\" IS NULL) OR "
                + "(\"Status\" IN ('Released', 'Expired') AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NULL) OR "
                + "(\"Status\" = 'Consumed' AND \"TerminalAtUtc\" IS NOT NULL AND \"TerminalActor\" IS NOT NULL AND \"TerminalReason\" IS NOT NULL AND \"TerminalReference\" IS NOT NULL AND \"ConsumedMovementId\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_StockReservations_Terminal_Chronology",
                "\"TerminalAtUtc\" IS NULL OR (\"TerminalAtUtc\" >= \"ReservedAtUtc\" AND (\"Status\" <> 'Expired' OR \"TerminalAtUtc\" >= \"ExpiresAtUtc\"))");
            table.HasCheckConstraint(
                "CK_StockReservations_Evidence_NotBlank",
                "\"ReserveReason\" ~ '[^[:space:]]' AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' AND "
                + "(\"TerminalActor\" IS NULL OR \"TerminalActor\" ~ '[^[:space:]]') AND "
                + "(\"TerminalReason\" IS NULL OR \"TerminalReason\" ~ '[^[:space:]]') AND "
                + "(\"TerminalReference\" IS NULL OR \"TerminalReference\" ~ '[^[:space:]]')");
        });

        builder.HasKey(reservation => reservation.Id);
        builder.HasAlternateKey(reservation => new
        {
            reservation.Id,
            reservation.InventoryItemId,
            reservation.ProductId,
        })
            .HasName("AK_StockReservations_Id_InventoryItemId_ProductId");
        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(reservation => reservation.ReservedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.TerminalAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.ReserveReason)
            .HasMaxLength(InventoryLimits.ReasonLength)
            .IsRequired();
        builder.Property(reservation => reservation.ReserveReference)
            .HasMaxLength(InventoryLimits.ReferenceLength)
            .IsRequired();
        builder.Property(reservation => reservation.ReservedBy)
            .HasMaxLength(InventoryLimits.ActorLength)
            .IsRequired();
        builder.Property(reservation => reservation.TerminalReason)
            .HasMaxLength(InventoryLimits.ReasonLength);
        builder.Property(reservation => reservation.TerminalReference)
            .HasMaxLength(InventoryLimits.ReferenceLength);
        builder.Property(reservation => reservation.TerminalActor)
            .HasMaxLength(InventoryLimits.ActorLength);

        builder.HasIndex(reservation => reservation.CheckoutAttemptId)
            .HasDatabaseName("IX_StockReservations_CheckoutAttemptId");
        builder.HasIndex(reservation => new
        {
            reservation.InventoryItemId,
            reservation.Status,
            reservation.ExpiresAtUtc,
        })
            .HasDatabaseName("IX_StockReservations_InventoryItemId_Status_ExpiresAtUtc");
        builder.HasIndex(reservation => reservation.ConsumedMovementId)
            .IsUnique()
            .HasFilter("\"ConsumedMovementId\" IS NOT NULL")
            .HasDatabaseName("UX_StockReservations_ConsumedMovementId");

        builder.HasOne<InventoryItem>()
            .WithMany()
            .HasForeignKey(reservation => new
            {
                reservation.InventoryItemId,
                reservation.ProductId,
            })
            .HasPrincipalKey(item => new { item.Id, item.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<StockMovement>()
            .WithMany()
            .HasForeignKey(reservation => reservation.ConsumedMovementId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("FK_StockReservations_StockMovements_ConsumedMovementId");
    }
}
