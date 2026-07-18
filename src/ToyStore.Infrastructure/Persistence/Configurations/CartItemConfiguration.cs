using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Carts;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CartItemConfiguration : IEntityTypeConfiguration<CartItem>
{
    public void Configure(EntityTypeBuilder<CartItem> builder)
    {
        builder.ToTable("CartItems", table =>
        {
            table.HasCheckConstraint(
                "CK_CartItems_CartId_NotEmpty",
                "\"CartId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "CK_CartItems_ProductId_NotEmpty",
                "\"ProductId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint(
                "CK_CartItems_Quantity_Bounds",
                $"\"Quantity\" BETWEEN 1 AND {CartLimits.MaximumQuantityPerItem}");
        });

        builder.Property<Guid>("CartId");
        builder.HasKey("CartId", nameof(CartItem.ProductId));
        builder.HasIndex(item => item.ProductId)
            .HasDatabaseName("IX_CartItems_ProductId");
        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
