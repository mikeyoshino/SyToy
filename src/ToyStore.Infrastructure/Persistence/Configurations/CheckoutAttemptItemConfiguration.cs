using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CheckoutAttemptItemConfiguration : IEntityTypeConfiguration<CheckoutAttemptItem>
{
    public void Configure(EntityTypeBuilder<CheckoutAttemptItem> builder)
    {
        builder.ToTable("CheckoutAttemptItems", table =>
        {
            table.HasCheckConstraint("CK_CheckoutAttemptItems_Quantity_Positive", "\"Quantity\" > 0");
            table.HasCheckConstraint("CK_CheckoutAttemptItems_Amounts",
                "\"UnitPrice\" > 0 AND \"DepositAmount\" >= 0 AND \"BalanceAmount\" >= 0 AND \"LinePaymentAmount\" > 0");
            table.HasCheckConstraint("CK_CheckoutAttemptItems_SaleShape",
                "(\"SaleType\" = 'InStock' AND \"DepositAmount\" = 0 AND \"BalanceAmount\" = 0 AND \"PreOrderCloseAtUtc\" IS NULL AND \"DepositPolicy\" IS NULL) OR " +
                "(\"SaleType\" = 'PreOrder' AND \"DepositAmount\" > 0 AND \"BalanceAmount\" > 0 AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"DepositPolicy\" IS NOT NULL)");
        });
        builder.HasKey(x => x.Id);
        builder.Property<Guid>("CheckoutAttemptId");
        builder.HasIndex("CheckoutAttemptId", nameof(CheckoutAttemptItem.ProductId)).IsUnique();
        builder.HasIndex(x => x.ReservationId).IsUnique();
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
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.DepositAmount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAmount).HasPrecision(18, 2);
        builder.Property(x => x.LinePaymentAmount).HasPrecision(18, 2);
        builder.Property(x => x.PreOrderCloseAtUtc).HasColumnType("timestamp with time zone");
    }
}
