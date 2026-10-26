using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace RetailLab.Data.Configurations;

internal sealed class SyncInboxReceiptConfiguration : IEntityTypeConfiguration<SyncInboxReceipt>
{
    public void Configure(EntityTypeBuilder<SyncInboxReceipt> builder)
    {
        builder.ToTable("SyncInboxReceipts");

        builder.HasKey(receipt => receipt.OperationId);
        builder.Property(receipt => receipt.OperationId)
            .ValueGeneratedNever();

        builder.Property(receipt => receipt.Outcome)
            .IsRequired();

        builder.Property(receipt => receipt.RecordedResult);

        builder.Property(receipt => receipt.ProcessedAtUtc)
            .IsRequired();

        builder.Property(receipt => receipt.DeviceDisplayName)
            .HasMaxLength(SyncDevice.MaximumDisplayNameLength);
        builder.Property(receipt => receipt.ActorDisplayName)
            .HasMaxLength(SyncDevice.MaximumStaffDisplayNameLength);
        builder.Property(receipt => receipt.DeviceRole)
            .HasMaxLength(SyncDevice.MaximumRoleLength);
        builder.Property(receipt => receipt.OperationHash)
            .HasMaxLength(64)
            .IsFixedLength();
        builder.Property(receipt => receipt.ResultServerRevision);
        builder.Property(receipt => receipt.ResultSnapshot);

        builder.HasIndex(receipt => new { receipt.DeviceId, receipt.ProcessedAtUtc });
        builder.HasOne<SyncDevice>()
            .WithMany()
            .HasForeignKey(receipt => receipt.DeviceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
