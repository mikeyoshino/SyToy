using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Catalog;
using ToyStore.Domain.Products;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", table =>
        {
            table.HasCheckConstraint(
                "CK_Products_Slug_Format",
                "\"Slug\" ~ '^[a-z0-9]+(-[a-z0-9]+)*$'");
            table.HasCheckConstraint(
                "CK_Products_Offer_Matches_SaleType",
                "(\"SaleType\" = 'InStock' AND \"InStockPrice\" IS NOT NULL AND \"PreOrderFullPrice\" IS NULL AND \"PreOrderDepositAmount\" IS NULL AND \"PreOrderCloseAtUtc\" IS NULL AND \"PreOrderEstimatedArrivalMonth\" IS NULL AND \"PreOrderEstimatedArrivalYear\" IS NULL AND \"PreOrderTotalCapacity\" IS NULL AND \"PreOrderMaxPerCustomer\" IS NULL AND \"PreOrderBalancePaymentDays\" IS NULL) OR (\"SaleType\" = 'PreOrder' AND \"InStockPrice\" IS NULL AND \"PreOrderFullPrice\" IS NOT NULL AND \"PreOrderDepositAmount\" IS NOT NULL AND \"PreOrderCloseAtUtc\" IS NOT NULL AND \"PreOrderEstimatedArrivalMonth\" IS NOT NULL AND \"PreOrderEstimatedArrivalYear\" IS NOT NULL AND \"PreOrderTotalCapacity\" IS NOT NULL AND \"PreOrderMaxPerCustomer\" IS NOT NULL AND \"PreOrderBalancePaymentDays\" IS NOT NULL)");
            table.HasCheckConstraint(
                "CK_Products_InStock_Price",
                "\"InStockPrice\" IS NULL OR \"InStockPrice\" > 0");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_Amounts",
                "\"PreOrderFullPrice\" IS NULL OR (\"PreOrderFullPrice\" > 0 AND \"PreOrderDepositAmount\" > 0 AND \"PreOrderDepositAmount\" < \"PreOrderFullPrice\")");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_Capacity",
                "\"PreOrderTotalCapacity\" IS NULL OR (\"PreOrderTotalCapacity\" > 0 AND \"PreOrderMaxPerCustomer\" > 0 AND \"PreOrderMaxPerCustomer\" <= \"PreOrderTotalCapacity\")");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_EstimatedArrival",
                "\"PreOrderEstimatedArrivalMonth\" IS NULL OR (\"PreOrderEstimatedArrivalMonth\" BETWEEN 1 AND 12 AND \"PreOrderEstimatedArrivalYear\" BETWEEN 1 AND 9999)");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_BalancePaymentDays",
                "\"PreOrderBalancePaymentDays\" IS NULL OR \"PreOrderBalancePaymentDays\" > 0");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_CloseAfterCreated",
                "\"PreOrderCloseAtUtc\" IS NULL OR \"PreOrderCloseAtUtc\" > \"CreatedAtUtc\"");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_BangkokCloseTime",
                "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok')::time = TIME '23:59:59'");
            table.HasCheckConstraint(
                "CK_Products_PreOrder_EtaNotBeforeCloseMonth",
                "\"PreOrderCloseAtUtc\" IS NULL OR (\"PreOrderEstimatedArrivalYear\", \"PreOrderEstimatedArrivalMonth\") >= (EXTRACT(YEAR FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'), EXTRACT(MONTH FROM \"PreOrderCloseAtUtc\" AT TIME ZONE 'Asia/Bangkok'))");
            table.HasCheckConstraint("CK_Products_Version_Positive", "\"Version\" > 0");
            table.HasCheckConstraint(
                "CK_Products_Audit_Chronology",
                "\"UpdatedAtUtc\" >= \"CreatedAtUtc\" AND (\"PublishedAtUtc\" IS NULL OR \"PublishedAtUtc\" >= \"CreatedAtUtc\") AND (\"ArchivedAtUtc\" IS NULL OR \"ArchivedAtUtc\" >= \"PublishedAtUtc\")");
            table.HasCheckConstraint(
                "CK_Products_Lifecycle_Audit",
                "(\"Status\" = 'Draft' AND \"PublishedAtUtc\" IS NULL AND \"PublishedBy\" IS NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Published' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NULL AND \"ArchivedBy\" IS NULL) OR (\"Status\" = 'Archived' AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" IS NOT NULL AND \"ArchivedAtUtc\" IS NOT NULL AND \"ArchivedBy\" IS NOT NULL)");
        });

        builder.HasKey(product => product.Id);
        builder.Property(product => product.DisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(product => product.NormalizedDisplayName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(product => product.EnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(product => product.NormalizedEnglishName).HasMaxLength(CatalogConfiguration.NameLength).IsRequired();
        builder.Property(product => product.Description).IsRequired();
        builder.Property(product => product.ModelScale)
            .HasMaxLength(Product.MaximumModelScaleLength);
        builder.Property(product => product.Slug).HasMaxLength(CatalogConfiguration.SlugLength).IsRequired();
        builder.Property(product => product.SaleType).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(product => product.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(product => product.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(product => product.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(product => product.PublishedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(product => product.ArchivedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(product => product.CreatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(product => product.UpdatedBy).HasMaxLength(CatalogConfiguration.ActorLength).IsRequired();
        builder.Property(product => product.PublishedBy).HasMaxLength(CatalogConfiguration.ActorLength);
        builder.Property(product => product.ArchivedBy).HasMaxLength(CatalogConfiguration.ActorLength);
        builder.Property(product => product.Version)
            .HasField("_version")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnType("bigint")
            .HasDefaultValue(1L)
            .IsConcurrencyToken();

        builder.HasIndex(product => product.Slug).IsUnique().HasDatabaseName("UX_Products_Slug");
        builder.HasIndex(product => product.NormalizedDisplayName).IsUnique().HasDatabaseName("UX_Products_NormalizedDisplayName");
        builder.HasIndex(product => product.NormalizedEnglishName).IsUnique().HasDatabaseName("UX_Products_NormalizedEnglishName");
        builder.HasIndex(product => new { product.Status, product.SaleType }).HasDatabaseName("IX_Products_Status_SaleType");

        builder.HasOne<ProductCategory>().WithMany().HasForeignKey(product => product.ProductCategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Brand>().WithMany().HasForeignKey(product => product.BrandId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Universe>().WithMany().HasForeignKey(product => product.UniverseId).OnDelete(DeleteBehavior.Restrict);

        builder.OwnsOne(product => product.InStockOffer, offer =>
        {
            offer.Property(value => value.Price)
                .HasColumnName("InStockPrice")
                .HasColumnType("numeric")
                .HasConversion(CatalogConfiguration.MoneyConverter);
            offer.HasIndex(value => value.Price)
                .HasDatabaseName("IX_Products_InStockPrice");
        });
        builder.Navigation(product => product.InStockOffer)
            .HasField("_inStockOffer")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.OwnsOne(product => product.PreOrderOffer, offer =>
        {
            offer.Ignore(value => value.BalanceAmount);
            offer.Ignore(value => value.EstimatedArrival);
            offer.Property(value => value.FullPrice)
                .HasColumnName("PreOrderFullPrice")
                .HasColumnType("numeric")
                .HasConversion(CatalogConfiguration.MoneyConverter);
            offer.Property(value => value.DepositAmount)
                .HasColumnName("PreOrderDepositAmount")
                .HasColumnType("numeric")
                .HasConversion(CatalogConfiguration.MoneyConverter);
            offer.Property(value => value.CloseAtUtc)
                .HasColumnName("PreOrderCloseAtUtc")
                .HasColumnType("timestamp with time zone");
            offer.Property<int>("EstimatedArrivalMonth").HasColumnName("PreOrderEstimatedArrivalMonth");
            offer.Property<int>("EstimatedArrivalYear").HasColumnName("PreOrderEstimatedArrivalYear");
            offer.Property(value => value.TotalCapacity).HasColumnName("PreOrderTotalCapacity");
            offer.Property(value => value.MaxPerCustomer).HasColumnName("PreOrderMaxPerCustomer");
            offer.Property(value => value.BalancePaymentDays).HasColumnName("PreOrderBalancePaymentDays");
        });
        builder.Navigation(product => product.PreOrderOffer)
            .HasField("_preOrderOffer")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(product => product.Images)
            .WithOne()
            .HasForeignKey("ProductId")
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Images)
            .HasField("_images")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(product => product.Characters)
            .WithOne()
            .HasForeignKey(link => link.ProductId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(product => product.Characters)
            .HasField("_characters")
            .UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
