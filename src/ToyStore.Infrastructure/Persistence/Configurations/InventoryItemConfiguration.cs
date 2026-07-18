using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Inventory;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class InventoryItemConfiguration : IEntityTypeConfiguration<InventoryItem>
{
    public void Configure(EntityTypeBuilder<InventoryItem> builder)
    {
        builder.ToTable("InventoryItems", table =>
        {
            table.HasCheckConstraint(
                "CK_InventoryItems_OnHandQuantity_NonNegative",
                "\"OnHandQuantity\" >= 0");
            table.HasCheckConstraint(
                "CK_InventoryItems_HeldQuantity_Bounds",
                "\"HeldQuantity\" >= 0 AND \"HeldQuantity\" <= \"OnHandQuantity\"");
            table.HasCheckConstraint(
                "CK_InventoryItems_Version_Positive",
                "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_InventoryItems_Audit_Chronology",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_InventoryItems_Audit_Actors_NotBlank",
                "\"CreatedBy\" ~ '[^[:space:]]' AND \"UpdatedBy\" ~ '[^[:space:]]'");
        });

        builder.HasKey(item => item.Id);
        builder.HasAlternateKey(item => new { item.Id, item.ProductId })
            .HasName("AK_InventoryItems_Id_ProductId");
        builder.HasIndex(item => item.ProductId)
            .IsUnique()
            .HasDatabaseName("UX_InventoryItems_ProductId");

        builder.Ignore(item => item.ReservableQuantity);
        builder.Property(item => item.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(item => item.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(item => item.CreatedBy)
            .HasMaxLength(InventoryLimits.ActorLength)
            .IsRequired();
        builder.Property(item => item.UpdatedBy)
            .HasMaxLength(InventoryLimits.ActorLength)
            .IsRequired();
        builder.Property(item => item.Version)
            .HasColumnType("bigint")
            .IsConcurrencyToken();

        builder.HasOne<Product>()
            .WithOne()
            .HasForeignKey<InventoryItem>(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
