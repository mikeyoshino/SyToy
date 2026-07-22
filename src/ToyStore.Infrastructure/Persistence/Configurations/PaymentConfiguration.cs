using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Orders;
using ToyStore.Domain.Checkouts;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class PaymentConfiguration : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.CheckoutAttemptId).IsUnique();
        builder.HasIndex(x => x.ProviderSessionId).IsUnique();
        builder.HasIndex(x => x.ProviderPaymentReference).IsUnique();
        builder.HasIndex(x => x.ProviderEventId).IsUnique();
        builder.HasIndex(x => x.PaidAtUtc)
            .HasDatabaseName("IX_Payments_PaidAtUtc");
        builder.Property(x => x.Purpose).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Currency).HasMaxLength(3).IsRequired();
        builder.Property(x => x.ProviderSessionId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProviderPaymentReference).HasMaxLength(255).IsRequired();
        builder.Property(x => x.ProviderEventId).HasMaxLength(255).IsRequired();
        builder.Property(x => x.PaidAtUtc).HasColumnType("timestamp with time zone");
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<CheckoutAttempt>().WithOne().HasForeignKey<Payment>(x => x.CheckoutAttemptId).OnDelete(DeleteBehavior.Restrict);
    }
}
