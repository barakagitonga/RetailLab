using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RetailLab.Core;

namespace RetailLab.Data.Configurations;

internal sealed class BookmarkConfiguration : IEntityTypeConfiguration<Bookmark>
{
    public void Configure(EntityTypeBuilder<Bookmark> builder)
    {
        builder.ToTable("Bookmarks");

        builder.HasKey(bookmark => new { bookmark.CustomerIdentifier, bookmark.ProductId });

        builder.Property(bookmark => bookmark.CustomerIdentifier)
            .HasMaxLength(CustomerIdentifier.MaximumLength)
            .IsRequired();

        builder.Property(bookmark => bookmark.CreatedAtUtc).IsRequired();

        builder.HasOne(bookmark => bookmark.Product)
            .WithMany()
            .HasForeignKey(bookmark => bookmark.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
