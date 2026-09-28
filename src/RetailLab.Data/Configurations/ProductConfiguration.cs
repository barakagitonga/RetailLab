using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class ProductConfiguration : IEntityTypeConfiguration<Product>
{
    public void Configure(EntityTypeBuilder<Product> builder)
    {
        builder.ToTable("Products", tableBuilder =>
        {
            tableBuilder.HasCheckConstraint("CK_Products_Price", "Price >= 0");
            tableBuilder.HasCheckConstraint("CK_Products_StockQuantity", "StockQuantity >= 0");
        });

        builder.HasKey(product => product.Id);

        builder.Property(product => product.Sku)
            .HasMaxLength(Product.MaximumSkuLength)
            .UseCollation("NOCASE")
            .IsRequired();

        builder.HasIndex(product => product.Sku).IsUnique();

        builder.Property(product => product.Description)
            .HasMaxLength(Product.MaximumDescriptionLength)
            .IsRequired();

        builder.Property(product => product.Price)
            .HasPrecision(18, 2)
            .IsRequired();

        builder.Property(product => product.StockQuantity).IsRequired();

        builder.Property(product => product.IsArchived).IsRequired();

        builder.Property(product => product.ArchivedAtUtc);
    }
}
