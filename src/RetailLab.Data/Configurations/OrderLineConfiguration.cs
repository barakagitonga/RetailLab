using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class OrderLineConfiguration : IEntityTypeConfiguration<OrderLine>
{
    public void Configure(EntityTypeBuilder<OrderLine> builder)
    {
        builder.ToTable("OrderLines", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_OrderLines_Quantity", "Quantity > 0");
            tableBuilder.HasCheckConstraint("CK_OrderLines_UnitPrice", "UnitPrice >= 0");
        });

        builder.HasKey(line => line.Id);

        builder.Property(line => line.Sku)
            .HasMaxLength(Product.MaximumSkuLength)
            .IsRequired();

        builder.Property(line => line.ProductDescription)
            .HasMaxLength(Product.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(line => line.UnitPrice)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(line => line.Quantity).IsRequired();
        builder.Ignore(line => line.LineTotal);

        builder.HasOne<Product>()
            .WithMany()
            .HasForeignKey(line => line.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
