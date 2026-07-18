using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class OrderItemConfiguration : IEntityTypeConfiguration<OrderItem>
{
    public void Configure(EntityTypeBuilder<OrderItem> builder)
    {
        builder.ToTable("OrderItems", table =>
        {
            table.HasCheckConstraint("CK_OrderItems_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_OrderItems_Amounts",
                "\"FullPrice\" > 0 AND \"DepositAmount\" >= 0 AND \"BalanceAmount\" >= 0 AND \"LinePaidAmount\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property<Guid>("OrderId");
        builder.HasIndex("OrderId", nameof(OrderItem.ProductId)).IsUnique();
        builder.HasOne<Product>().WithMany().HasForeignKey(x => x.ProductId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.SaleType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.EnglishName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.ProductSlug).HasMaxLength(240).IsRequired();
        builder.Property(x => x.CategoryName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.BrandName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UniverseName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.PrimaryImageUrl).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.DepositPolicy).HasMaxLength(100);
        builder.Property(x => x.FullPrice).HasPrecision(18, 2);
        builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAmount).HasPrecision(18, 2);
        builder.Property(x => x.LinePaidAmount).HasPrecision(18, 2);
        builder.Property(x => x.PreOrderCloseAtUtc).HasColumnType("timestamp with time zone");
    }
}
