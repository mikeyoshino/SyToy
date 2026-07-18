using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Checkouts;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CheckoutAttemptConfiguration : IEntityTypeConfiguration<CheckoutAttempt>
{
    public void Configure(EntityTypeBuilder<CheckoutAttempt> builder)
    {
        builder.ToTable("CheckoutAttempts", table =>
        {
            table.HasCheckConstraint("CK_CheckoutAttempts_PaymentAmount_Positive", "\"PaymentAmount\" > 0");
            table.HasCheckConstraint("CK_CheckoutAttempts_ShippingAmount_NonNegative", "\"ShippingAmount\" >= 0");
            table.HasCheckConstraint("CK_CheckoutAttempts_Expiry", "\"ExpiresAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint("CK_CheckoutAttempts_Version", "\"Version\" > 0");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).HasMaxLength(450).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.CustomerId, x.IdempotencyKey }).IsUnique()
            .HasDatabaseName("UX_CheckoutAttempts_CustomerId_IdempotencyKey");
        builder.HasIndex(x => x.ProviderSessionId).IsUnique().HasFilter("\"ProviderSessionId\" IS NOT NULL")
            .HasDatabaseName("UX_CheckoutAttempts_ProviderSessionId");
        builder.Property(x => x.IdempotencyKey).HasMaxLength(100).IsRequired();
        builder.Property(x => x.ProviderSessionId).HasMaxLength(255);
        builder.Property(x => x.SaleType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.PaymentAmount).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ExpiresAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.Version).IsConcurrencyToken();
        ConfigureAddress(builder.OwnsOne(x => x.Address));
        builder.HasMany(x => x.Items).WithOne().HasForeignKey("CheckoutAttemptId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(x => x.ProductId);
        builder.Ignore(x => x.CapacityId);
        builder.Ignore(x => x.ReservationId);
        builder.Ignore(x => x.Quantity);
        builder.Ignore(x => x.DisplayName);
        builder.Ignore(x => x.EnglishName);
        builder.Ignore(x => x.ProductSlug);
        builder.Ignore(x => x.CategoryName);
        builder.Ignore(x => x.BrandName);
        builder.Ignore(x => x.UniverseName);
        builder.Ignore(x => x.PrimaryImageUrl);
        builder.Ignore(x => x.FullPrice);
        builder.Ignore(x => x.DepositAmount);
        builder.Ignore(x => x.BalanceAmount);
        builder.Ignore(x => x.PreOrderCloseAtUtc);
        builder.Ignore(x => x.EstimatedArrivalMonth);
        builder.Ignore(x => x.EstimatedArrivalYear);
        builder.Ignore(x => x.BalancePaymentDays);
        builder.Ignore(x => x.DepositPolicy);
    }

    internal static void ConfigureAddress(OwnedNavigationBuilder<CheckoutAttempt, ShippingAddressSnapshot> address)
    {
        address.Property(x => x.RecipientName).HasColumnName("RecipientName").HasMaxLength(200).IsRequired();
        address.Property(x => x.PhoneNumber).HasColumnName("PhoneNumber").HasMaxLength(32).IsRequired();
        address.Property(x => x.AddressLine).HasColumnName("AddressLine").HasMaxLength(500).IsRequired();
        address.Property(x => x.SubDistrict).HasColumnName("SubDistrict").HasMaxLength(200).IsRequired();
        address.Property(x => x.District).HasColumnName("District").HasMaxLength(200).IsRequired();
        address.Property(x => x.Province).HasColumnName("Province").HasMaxLength(200).IsRequired();
        address.Property(x => x.PostalCode).HasColumnName("PostalCode").HasMaxLength(5).IsRequired();
    }
}
