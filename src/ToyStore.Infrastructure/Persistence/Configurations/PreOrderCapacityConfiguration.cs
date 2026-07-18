using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.PreOrders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class PreOrderCapacityConfiguration
    : IEntityTypeConfiguration<PreOrderCapacity>
{
    public void Configure(EntityTypeBuilder<PreOrderCapacity> builder)
    {
        builder.ToTable("PreOrderCapacities", table =>
        {
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_TotalCapacity_Positive",
                "\"TotalCapacity\" > 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_QuantityAccounting",
                "\"HeldQuantity\" >= 0 AND \"CommittedQuantity\" >= 0 "
                + "AND \"RetiredQuantity\" >= 0 "
                + "AND \"HeldQuantity\" + \"CommittedQuantity\" + \"RetiredQuantity\" <= \"TotalCapacity\"");
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_Version_Positive",
                "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_Audit_Chronology",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_CloseAfterCreated",
                "\"CloseAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_PreOrderCapacities_Audit_Actors_NotBlank",
                "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
        });

        builder.HasKey(capacity => capacity.Id);
        builder.HasAlternateKey(capacity => new { capacity.Id, capacity.ProductId })
            .HasName("AK_PreOrderCapacities_Id_ProductId");
        builder.HasIndex(capacity => capacity.ProductId)
            .IsUnique()
            .HasDatabaseName("UX_PreOrderCapacities_ProductId");

        builder.Ignore(capacity => capacity.RemainingQuantity);
        builder.Property(capacity => capacity.CloseAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(capacity => capacity.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(capacity => capacity.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(capacity => capacity.CreatedBy)
            .HasMaxLength(PreOrderCapacityLimits.ActorLength)
            .IsRequired();
        builder.Property(capacity => capacity.UpdatedBy)
            .HasMaxLength(PreOrderCapacityLimits.ActorLength)
            .IsRequired();
        builder.Property(capacity => capacity.Version)
            .HasColumnType("bigint")
            .IsConcurrencyToken();

        builder.HasOne<Product>()
            .WithOne()
            .HasForeignKey<PreOrderCapacity>(capacity => capacity.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
