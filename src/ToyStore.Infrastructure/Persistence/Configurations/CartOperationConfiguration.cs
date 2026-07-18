using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ToyStore.Domain.Carts;

namespace ToyStore.Infrastructure.Persistence.Configurations;

internal sealed class CartOperationConfiguration : IEntityTypeConfiguration<CartOperation>
{
    public void Configure(EntityTypeBuilder<CartOperation> builder)
    {
        builder.ToTable("CartOperations", table =>
        {
            table.HasCheckConstraint("CK_CartOperations_Id_NotEmpty",
                "\"Id\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_CartOperations_CartId_NotEmpty",
                "\"CartId\" <> '00000000-0000-0000-0000-000000000000'::uuid");
            table.HasCheckConstraint("CK_CartOperations_Fingerprint",
                $"\"IntentFingerprint\" ~ '^[0-9a-f]{{{CartOperation.FingerprintLength}}}$'");
            table.HasCheckConstraint("CK_CartOperations_ResultingVersion_Positive",
                "\"ResultingCartVersion\" > 0");
            table.HasCheckConstraint("CK_CartOperations_ResultingTotal_NonNegative",
                "\"ResultingTotalQuantity\" >= 0");
            table.HasCheckConstraint("CK_CartOperations_ResultData_ByType",
                "(\"Type\" = 'Merge' AND \"ResultData\" IS NOT NULL) OR "
                + "(\"Type\" <> 'Merge' AND \"ResultData\" IS NULL)");
        });
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Type)
            .HasConversion<string>().HasMaxLength(30).IsRequired();
        builder.Property(operation => operation.IntentFingerprint)
            .HasMaxLength(CartOperation.FingerprintLength).IsFixedLength().IsRequired();
        builder.Property(operation => operation.ResultData).HasColumnType("jsonb");
        builder.Property(operation => operation.OccurredAtUtc)
            .HasColumnType("timestamp with time zone");
        builder.HasIndex(operation => new { operation.CartId, operation.OccurredAtUtc })
            .HasDatabaseName("IX_CartOperations_CartId_OccurredAtUtc");
        builder.HasOne<Cart>().WithMany().HasForeignKey(operation => operation.CartId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
