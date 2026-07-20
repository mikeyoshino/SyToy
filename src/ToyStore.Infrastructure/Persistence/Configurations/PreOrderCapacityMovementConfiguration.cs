using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class PreOrderCapacityMovementConfiguration
    : IEntityTypeConfiguration<PreOrderCapacityMovement>
{
    public void Configure(EntityTypeBuilder<PreOrderCapacityMovement> builder)
    {
        builder.ToTable("PreOrderCapacityMovements", table =>
        {
            table.HasCheckConstraint(
                "CK_PreOrderCapacityMovements_Quantity_Positive",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityMovements_Type_Evidence",
                "(\"Type\" = 'InitialCapacity' AND \"AvailableQuantityDelta\" = \"Quantity\" "
                + "AND \"ReservationId\" IS NULL AND \"ResultingCapacityVersion\" = 1 "
                + "AND \"ResultingRemainingQuantity\" = \"Quantity\" "
                + "AND \"ResultingHeldQuantity\" = 0 AND \"ResultingCommittedQuantity\" = 0 "
                + "AND \"ResultingRetiredQuantity\" = 0) OR "
                + "(\"Type\" = 'Reserved' AND \"AvailableQuantityDelta\" = -\"Quantity\" AND \"ReservationId\" IS NOT NULL) OR "
                + "(\"Type\" IN ('Released', 'Expired', 'CancellationReopened') "
                + "AND \"AvailableQuantityDelta\" = \"Quantity\" AND \"ReservationId\" IS NOT NULL) OR "
                + "(\"Type\" IN ('ReservationConsumed', 'CancellationRetired') "
                + "AND \"AvailableQuantityDelta\" = 0 AND \"ReservationId\" IS NOT NULL) OR "
                + "(\"Type\" = 'CapacityIncreased' AND \"AvailableQuantityDelta\" = \"Quantity\" "
                + "AND \"ReservationId\" IS NULL) OR "
                + "(\"Type\" = 'CapacityDecreased' AND \"AvailableQuantityDelta\" = -\"Quantity\" "
                + "AND \"ReservationId\" IS NULL)");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityMovements_ResultingQuantities_NonNegative",
                "\"ResultingRemainingQuantity\" >= 0 AND \"ResultingHeldQuantity\" >= 0 "
                + "AND \"ResultingCommittedQuantity\" >= 0 AND \"ResultingRetiredQuantity\" >= 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityMovements_ResultingVersion_Positive",
                "\"ResultingCapacityVersion\" > 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityMovements_Evidence_NotBlank",
                "\"Reason\" ~ '[^[:space:]]' AND \"Reference\" ~ '[^[:space:]]' AND \"Actor\" ~ '[^[:space:]]'");
        });

        builder.HasKey(movement => movement.Id);
        builder.Property(movement => movement.Type)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(movement => movement.Reason)
            .HasMaxLength(PreOrderCapacityLimits.ReasonLength)
            .IsRequired();
        builder.Property(movement => movement.Reference)
            .HasMaxLength(PreOrderCapacityLimits.ReferenceLength)
            .IsRequired();
        builder.Property(movement => movement.Actor)
            .HasMaxLength(PreOrderCapacityLimits.ActorLength)
            .IsRequired();
        builder.Property(movement => movement.OccurredAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasIndex(movement => new
        {
            movement.CapacityId,
            movement.ResultingCapacityVersion,
        })
            .IsUnique()
            .HasDatabaseName("UX_PreOrderCapacityMovements_CapacityId_Version");
        builder.HasIndex(movement => movement.CapacityId)
            .IsUnique()
            .HasFilter("\"Type\" = 'InitialCapacity'")
            .HasDatabaseName("UX_PreOrderCapacityMovements_CapacityId_InitialCapacity");
        builder.HasIndex(movement => new
        {
            movement.CapacityId,
            movement.OccurredAtUtc,
            movement.Id,
        })
            .IsDescending(false, true, true)
            .HasDatabaseName("IX_PreOrderCapacityMovements_CapacityId_OccurredAtUtc_Id");

        builder.HasOne<PreOrderCapacity>()
            .WithMany()
            .HasForeignKey(movement => new
            {
                movement.CapacityId,
                movement.ProductId,
            })
            .HasPrincipalKey(capacity => new { capacity.Id, capacity.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<PreOrderCapacityReservation>()
            .WithMany()
            .HasForeignKey(
                nameof(PreOrderCapacityMovement.ReservationId),
                nameof(PreOrderCapacityMovement.CapacityId),
                nameof(PreOrderCapacityMovement.ProductId))
            .HasPrincipalKey(
                nameof(PreOrderCapacityReservation.Id),
                nameof(PreOrderCapacityReservation.CapacityId),
                nameof(PreOrderCapacityReservation.ProductId))
            .OnDelete(DeleteBehavior.Restrict);
    }
}
