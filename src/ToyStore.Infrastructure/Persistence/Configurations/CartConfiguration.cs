using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Carts;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CartConfiguration : IEntityTypeConfiguration<Cart>
{
    public void Configure(EntityTypeBuilder<Cart> builder)
    {
        builder.ToTable("Carts", table =>
        {
            table.HasCheckConstraint(
                "CK_Carts_Id_NotEmpty",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "CK_Carts_CustomerId_NotBlank",
                "\"CustomerId\" ~ '[^[:space:]]'");
            table.HasCheckConstraint(
                "CK_Carts_Version_Positive",
                "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_Carts_Audit_Chronology",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });

        builder.HasKey(cart => cart.Id);
        builder.Property(cart => cart.CustomerId)
            .HasMaxLength(CartLimits.CustomerIdentityLength)
            .IsRequired();
        builder.HasIndex(cart => cart.CustomerId)
            .IsUnique()
            .HasDatabaseName("UX_Carts_CustomerId");
        builder.Property(cart => cart.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(cart => cart.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.Property(cart => cart.Version)
            .HasColumnType("bigint")
            .IsConcurrencyToken();

        builder.HasOne<ApplicationUser>()
            .WithMany()
            .HasForeignKey(cart => cart.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(cart => cart.Items)
            .WithOne()
            .HasForeignKey("CartId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(cart => cart.Items)
            .HasField("_items")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
