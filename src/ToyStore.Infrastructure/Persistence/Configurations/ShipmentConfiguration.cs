using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Orders;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class ShipmentConfiguration : IEntityTypeConfiguration<Shipment>
{
    public void Configure(EntityTypeBuilder<Shipment> builder)
    {
        builder.ToTable("Shipments"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderId).IsUnique(); builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasIndex(x => new { x.Carrier, x.TrackingNumber }).IsUnique();
        builder.HasOne<Order>().WithOne().HasForeignKey<Shipment>(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Carrier).HasConversion<string>().HasMaxLength(24).IsRequired();
        builder.Property(x => x.TrackingNumber).HasMaxLength(100).IsRequired();
        builder.Property(x => x.TrackingUrl).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ShippedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedBy).HasMaxLength(450).IsRequired();
    }
}

internal sealed class OrderAuditEventConfiguration : IEntityTypeConfiguration<OrderAuditEvent>
{
    public void Configure(EntityTypeBuilder<OrderAuditEvent> builder)
    {
        builder.ToTable("OrderAuditEvents"); builder.HasKey(x => x.Id);
        builder.HasIndex(x => x.OrderId); builder.HasIndex(x => x.OperationId).IsUnique();
        builder.HasOne<Order>().WithMany().HasForeignKey(x => x.OrderId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Action).HasMaxLength(80).IsRequired();
        builder.Property(x => x.ActorId).HasMaxLength(450).IsRequired();
        builder.Property(x => x.Detail).HasMaxLength(500).IsRequired();
        builder.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
    }
}
