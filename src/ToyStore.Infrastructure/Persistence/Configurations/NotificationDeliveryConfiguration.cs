using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Notifications;
using ToyStore.Domain.Orders;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class NotificationDeliveryConfiguration
    : IEntityTypeConfiguration<NotificationDelivery>
{
    public void Configure(EntityTypeBuilder<NotificationDelivery> builder)
    {
        builder.ToTable("NotificationDeliveries");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();
        builder.HasIndex(x => new { x.Status, x.LastAttemptedAtUtc });
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Type).HasMaxLength(80).IsRequired();
        builder.Property(x => x.RecipientKey).HasMaxLength(255).IsRequired();
        builder.Property(x => x.IdempotencyKey).HasMaxLength(255).IsRequired();
        builder.Property(x => x.Payload).HasMaxLength(4096).IsRequired();
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.SafeProviderResponse).HasMaxLength(500);
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastAttemptedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeliveredAtUtc).HasColumnType("timestamp with time zone");
    }
}
