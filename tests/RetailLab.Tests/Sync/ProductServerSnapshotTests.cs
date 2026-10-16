using RetailLab.Core;

namespace RetailLab.Tests.Sync;

public sealed class ProductServerSnapshotTests
{
    private static readonly DateTimeOffset OccurredAt =
        new(2026, 10, 16, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void CreateFromServerSnapshot_PreservesAuthoritativeIdentityAndRevision()
    {
        var productId = Guid.NewGuid();
        var entry = CreateEntry(1, productId, 1, " SKU-1 ", " Server product ", 12.50m, 8);

        var product = Product.CreateFromServerSnapshot(entry);

        Assert.Equal(productId, product.Id);
        Assert.Equal("SKU-1", product.Sku);
        Assert.Equal("Server product", product.Description);
        Assert.Equal(1, product.ServerRevision);
        Assert.Equal(0, product.Version);
    }

    [Fact]
    public void ApplyServerSnapshot_ReplacesProjectionAndAdvancesLocalVersionOnce()
    {
        var productId = Guid.NewGuid();
        var product = Product.CreateFromServerSnapshot(
            CreateEntry(1, productId, 1, "SKU-1", "Original", 10m, 5));
        var archivedAt = OccurredAt.AddMinutes(2);
        var update = CreateEntry(
            2,
            productId,
            2,
            "SKU-1",
            "Archived by server",
            11m,
            4,
            isArchived: true,
            archivedAtUtc: archivedAt);

        product.ApplyServerSnapshot(update);

        Assert.Equal("Archived by server", product.Description);
        Assert.Equal(11m, product.Price);
        Assert.Equal(4, product.StockQuantity);
        Assert.True(product.IsArchived);
        Assert.Equal(archivedAt, product.ArchivedAtUtc);
        Assert.Equal(2, product.ServerRevision);
        Assert.Equal(1, product.Version);
    }

    [Fact]
    public void ApplyServerSnapshot_RejectsSkippedRevisionWithoutMutation()
    {
        var productId = Guid.NewGuid();
        var product = Product.CreateFromServerSnapshot(
            CreateEntry(1, productId, 1, "SKU-1", "Original", 10m, 5));
        var skippedRevision = CreateEntry(2, productId, 3, "SKU-1", "Wrong", 99m, 1);

        Assert.Throws<BusinessRuleException>(() => product.ApplyServerSnapshot(skippedRevision));

        Assert.Equal("Original", product.Description);
        Assert.Equal(10m, product.Price);
        Assert.Equal(1, product.ServerRevision);
        Assert.Equal(0, product.Version);
    }

    [Fact]
    public void CreateFromServerSnapshot_RejectsFeedAndSnapshotIdentityMismatch()
    {
        var entry = CreateEntry(1, Guid.NewGuid(), 1, "SKU-1", "Product", 10m, 5);
        var mismatched = entry with { ProductId = Guid.NewGuid() };

        Assert.Throws<BusinessRuleException>(() => Product.CreateFromServerSnapshot(mismatched));
    }

    internal static ServerChangeEntry CreateEntry(
        long sequence,
        Guid productId,
        long revision,
        string sku = "SKU-1",
        string description = "Product",
        decimal price = 10m,
        int stock = 5,
        bool isArchived = false,
        DateTimeOffset? archivedAtUtc = null)
    {
        var snapshot = new ProductSnapshot(
            productId,
            sku,
            description,
            price,
            stock,
            isArchived,
            archivedAtUtc,
            revision);

        return new ServerChangeEntry(
            sequence,
            productId,
            revision == 1 ? ProductChangeKind.Created : ProductChangeKind.Updated,
            revision,
            snapshot,
            OccurredAt.AddMinutes(sequence));
    }
}
