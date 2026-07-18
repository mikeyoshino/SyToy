using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Checkouts;
using ToyStore.Domain.Orders;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Number).HasMaxLength(40).IsRequired();
        builder.HasIndex(x => x.Number).IsUnique();
        builder.HasIndex(x => x.CheckoutAttemptId).IsUnique();
        builder.HasOne<CheckoutAttempt>().WithOne().HasForeignKey<Order>(x => x.CheckoutAttemptId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.CustomerId).HasMaxLength(450).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.SaleType).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(x => x.PaymentStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(x => x.FulfillmentStatus).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.ShippingAmount).HasPrecision(18, 2);
        builder.Property(x => x.TotalPaid).HasPrecision(18, 2);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        ConfigureAddress(builder.OwnsOne(x => x.Address));
        builder.HasMany(x => x.Items).WithOne().HasForeignKey("OrderId").OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Items).HasField("_items").UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Ignore(x => x.Item);
    }

    private static void ConfigureAddress(OwnedNavigationBuilder<Order, ShippingAddressSnapshot> address)
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
