using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("InventoryAdjustments", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_InventoryAdjustments_QuantityChange", "QuantityChange <> 0");
            tableBuilder.HasCheckConstraint("CK_InventoryAdjustments_ResultingQuantity", "ResultingQuantity >= 0");
        });

        builder.HasKey(adjustment => adjustment.Id);

        builder.Property(adjustment => adjustment.QuantityChange).IsRequired();

        builder.Property(adjustment => adjustment.ResultingQuantity).IsRequired();

        builder.Property(adjustment => adjustment.Reason)
            .HasMaxLength(InventoryAdjustment.MaximumReasonLength)
            .IsRequired();

        builder.Property(adjustment => adjustment.ActorIdentifier)
            .HasMaxLength(InventoryAdjustment.MaximumActorLength)
            .IsRequired();

        builder.Property(adjustment => adjustment.CreatedAtUtc).IsRequired();

        builder.HasOne(adjustment => adjustment.Product)
            .WithMany()
            .HasForeignKey(adjustment => adjustment.ProductId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(adjustment => new { adjustment.ProductId, adjustment.CreatedAtUtc });
    }
}
