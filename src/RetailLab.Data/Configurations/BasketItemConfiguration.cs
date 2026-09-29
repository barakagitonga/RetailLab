using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class BasketItemConfiguration : IEntityTypeConfiguration<BasketItem>
{
    public void Configure(EntityTypeBuilder<BasketItem> builder)
    {
        builder.ToTable("BasketItems", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_BasketItems_Quantity", "Quantity > 0");
        });

        builder.HasKey(item => new { item.CustomerIdentifier, item.ProductId });

        builder.Property(item => item.CustomerIdentifier)
            .HasMaxLength(CustomerIdentifier.MaximumLength)
            .IsRequired();

        builder.Property(item => item.Quantity).IsRequired();

        builder.Property(item => item.CreatedAtUtc).IsRequired();

        builder.Property(item => item.UpdatedAtUtc).IsRequired();

        builder.Property(item => item.Version)
            .IsConcurrencyToken()
            .IsRequired();

        builder.HasOne(item => item.Product)
            .WithMany()
            .HasForeignKey(item => item.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
