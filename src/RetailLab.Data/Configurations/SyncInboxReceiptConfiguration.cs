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
    }
}
