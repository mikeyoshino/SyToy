using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Addresses;
using ToyStore.Domain.Checkouts;
using ToyStore.Infrastructure.Identity;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class SavedAddressConfiguration : IEntityTypeConfiguration<SavedAddress>
{
    public void Configure(EntityTypeBuilder<SavedAddress> builder)
    {
        builder.ToTable("SavedAddresses", table =>
        {
            table.HasCheckConstraint("CK_SavedAddresses_LocationIds_Positive",
                "\"ProvinceId\" > 0 AND \"DistrictId\" > 0 AND \"SubDistrictId\" > 0");
            table.HasCheckConstraint("CK_SavedAddresses_UpdatedAfterCreated",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\"");
        });
        builder.HasKey(x => x.Id);
        builder.Property(x => x.CustomerId).HasMaxLength(450).IsRequired();
        builder.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CustomerId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Property(x => x.Label).HasMaxLength(80).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => x.CustomerId);
        builder.HasIndex(x => x.CustomerId).IsUnique().HasFilter("\"IsDefault\"")
            .HasDatabaseName("UX_SavedAddresses_Customer_Default");

        ConfigureAddress(builder.OwnsOne(x => x.Address));
    }

    private static void ConfigureAddress(
        OwnedNavigationBuilder<SavedAddress, ShippingAddressSnapshot> address)
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
