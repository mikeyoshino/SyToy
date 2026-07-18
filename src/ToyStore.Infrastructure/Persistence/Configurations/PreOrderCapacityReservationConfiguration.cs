using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.PreOrders;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class PreOrderCapacityReservationConfiguration
    : IEntityTypeConfiguration<PreOrderCapacityReservation>
{
    public void Configure(EntityTypeBuilder<PreOrderCapacityReservation> builder)
    {
        builder.ToTable("PreOrderCapacityReservations", table =>
        {
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_Quantity_Positive",
                "\"Quantity\" > 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_Expiry_AfterReserved",
                "\"ExpiresAtUtc\" > \"ReservedAtUtc\"");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_Lifecycle_Evidence",
                "(\"Status\" = 'Active' AND \"TransitionAtUtc\" IS NULL AND \"TransitionActor\" IS NULL "
                + "AND \"TransitionReason\" IS NULL AND \"TransitionReference\" IS NULL "
                + "AND \"TransitionMovementId\" IS NULL AND \"ConsumedMovementId\" IS NULL "
                + "AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR "
                + "(\"Status\" IN ('Released', 'Expired') AND \"TransitionAtUtc\" IS NOT NULL "
                + "AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL "
                + "AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL "
                + "AND \"ConsumedMovementId\" IS NULL AND \"CancellationKind\" IS NULL "
                + "AND \"DepositDisposition\" IS NULL) OR "
                + "(\"Status\" = 'Consumed' AND \"TransitionAtUtc\" IS NOT NULL "
                + "AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL "
                + "AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL "
                + "AND \"ConsumedMovementId\" = \"TransitionMovementId\" "
                + "AND \"CancellationKind\" IS NULL AND \"DepositDisposition\" IS NULL) OR "
                + "(\"Status\" = 'Cancelled' AND \"TransitionAtUtc\" IS NOT NULL "
                + "AND \"TransitionActor\" IS NOT NULL AND \"TransitionReason\" IS NOT NULL "
                + "AND \"TransitionReference\" IS NOT NULL AND \"TransitionMovementId\" IS NOT NULL "
                + "AND \"ConsumedMovementId\" IS NOT NULL AND \"CancellationKind\" IS NOT NULL "
                + "AND \"DepositDisposition\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_CancellationPolicy",
                "\"Status\" <> 'Cancelled' OR "
                + "((\"CancellationKind\" IN ('Customer', 'BalanceOverdue') AND \"DepositDisposition\" = 'Forfeited') "
                + "OR (\"CancellationKind\" = 'AdminOrSupplier' AND \"DepositDisposition\" = 'RefundRequired'))");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_Transition_Chronology",
                "\"TransitionAtUtc\" IS NULL OR (\"TransitionAtUtc\" >= \"ReservedAtUtc\" "
                + "AND (\"Status\" <> 'Expired' OR \"TransitionAtUtc\" >= \"ExpiresAtUtc\"))");
            table.HasCheckConstraint(
                "CK_PreOrderCapacityReservations_Evidence_NotBlank",
                "\"CustomerId\" ~ '[^[:space:]]' AND \"ReserveReason\" ~ '[^[:space:]]' "
                + "AND \"ReserveReference\" ~ '[^[:space:]]' AND \"ReservedBy\" ~ '[^[:space:]]' "
                + "AND (\"TransitionActor\" IS NULL OR \"TransitionActor\" ~ '[^[:space:]]') "
                + "AND (\"TransitionReason\" IS NULL OR \"TransitionReason\" ~ '[^[:space:]]') "
                + "AND (\"TransitionReference\" IS NULL OR \"TransitionReference\" ~ '[^[:space:]]')");
        });

        builder.HasKey(reservation => reservation.Id);
        builder.HasAlternateKey(reservation => new
        {
            reservation.Id,
            reservation.CapacityId,
            reservation.ProductId,
        })
            .HasName("AK_PreOrderCapacityReservations_Id_CapacityId_ProductId");
        builder.Property(reservation => reservation.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(reservation => reservation.CancellationKind)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(reservation => reservation.DepositDisposition)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(reservation => reservation.CustomerId)
            .HasMaxLength(PreOrderCapacityLimits.CustomerIdLength)
            .IsRequired();
        builder.Property(reservation => reservation.ReservedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.ExpiresAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.TransitionAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(reservation => reservation.ReserveReason)
            .HasMaxLength(PreOrderCapacityLimits.ReasonLength)
            .IsRequired();
        builder.Property(reservation => reservation.ReserveReference)
            .HasMaxLength(PreOrderCapacityLimits.ReferenceLength)
            .IsRequired();
        builder.Property(reservation => reservation.ReservedBy)
            .HasMaxLength(PreOrderCapacityLimits.ActorLength)
            .IsRequired();
        builder.Property(reservation => reservation.TransitionReason)
            .HasMaxLength(PreOrderCapacityLimits.ReasonLength);
        builder.Property(reservation => reservation.TransitionReference)
            .HasMaxLength(PreOrderCapacityLimits.ReferenceLength);
        builder.Property(reservation => reservation.TransitionActor)
            .HasMaxLength(PreOrderCapacityLimits.ActorLength);

        builder.HasIndex(reservation => reservation.CheckoutAttemptId)
            .IsUnique()
            .HasDatabaseName("UX_PreOrderCapacityReservations_CheckoutAttemptId");
        builder.HasIndex(reservation => reservation.ReserveMovementId)
            .IsUnique()
            .HasDatabaseName("UX_PreOrderCapacityReservations_ReserveMovementId");
        builder.HasIndex(reservation => reservation.TransitionMovementId)
            .IsUnique()
            .HasFilter("\"TransitionMovementId\" IS NOT NULL")
            .HasDatabaseName("UX_PreOrderCapacityReservations_TransitionMovementId");
        builder.HasIndex(reservation => new
        {
            reservation.ProductId,
            reservation.CustomerId,
            reservation.Status,
        })
            .HasDatabaseName("IX_PreOrderCapacityReservations_ProductId_CustomerId_Status");
        builder.HasIndex(reservation => new
        {
            reservation.CapacityId,
            reservation.Status,
            reservation.ExpiresAtUtc,
        })
            .HasDatabaseName("IX_PreOrderCapacityReservations_CapacityId_Status_ExpiresAtUtc");

        builder.HasOne<PreOrderCapacity>()
            .WithMany()
            .HasForeignKey(reservation => new
            {
                reservation.CapacityId,
                reservation.ProductId,
            })
            .HasPrincipalKey(capacity => new { capacity.Id, capacity.ProductId })
            .OnDelete(DeleteBehavior.Restrict);
    }
}
