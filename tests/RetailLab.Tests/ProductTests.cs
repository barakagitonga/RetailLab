using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class ProductTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_TrimsValuesAndStoresValidData()
    {
        var product = new Product(" SKU-1 ", " Sample product ", 12.50m, 5);

        Assert.Equal("SKU-1", product.Sku);
        Assert.Equal("Sample product", product.Description);
        Assert.Equal(12.50m, product.Price);
        Assert.Equal(5, product.StockQuantity);
        Assert.False(product.IsArchived);
        Assert.Null(product.ArchivedAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_RejectsBlankSku(string sku)
    {
        Assert.Throws<ArgumentException>(() => new Product(sku, "Product", 1m, 1));
    }

    [Fact]
    public void Constructor_RejectsNegativePrice()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Product("SKU-1", "Product", -0.01m, 1));
    }

    [Fact]
    public void Constructor_RejectsNegativeStock()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new Product("SKU-1", "Product", 1m, -1));
    }

    [Fact]
    public void ReduceStock_SubtractsAvailableQuantity()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);

        product.ReduceStock(3);

        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void ReduceStock_RejectsInsufficientStockWithoutChangingQuantity()
    {
        var product = new Product("SKU-1", "Product", 1m, 2);

        Assert.Throws<BusinessRuleException>(() => product.ReduceStock(3));
        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void AdjustStock_AcceptsPositiveAndNegativeDeltas()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);

        product.AdjustStock(3);
        Assert.Equal(8, product.StockQuantity);

        product.AdjustStock(-8);
        Assert.Equal(0, product.StockQuantity);
    }

    [Fact]
    public void AdjustStock_RejectsZeroDelta()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);

        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(0));
        Assert.Equal(5, product.StockQuantity);
    }

    [Fact]
    public void AdjustStock_RejectsNegativeResultWithoutChangingQuantity()
    {
        var product = new Product("SKU-1", "Product", 1m, 2);

        Assert.Throws<BusinessRuleException>(() => product.AdjustStock(-3));
        Assert.Equal(2, product.StockQuantity);
    }

    [Fact]
    public void UpdateDetails_ChangesDescriptionAndPrice()
    {
        var product = new Product("SKU-1", "Old", 1m, 5);

        product.UpdateDetails("New description", 2.50m);

        Assert.Equal("New description", product.Description);
        Assert.Equal(2.50m, product.Price);
        Assert.Equal("SKU-1", product.Sku);
    }

    [Fact]
    public void UpdateDetails_RejectsNegativePriceWithoutChangingDescription()
    {
        var product = new Product("SKU-1", "Original", 1m, 5);

        Assert.Throws<ArgumentOutOfRangeException>(() => product.UpdateDetails("Changed", -1m));
        Assert.Equal("Original", product.Description);
        Assert.Equal(1m, product.Price);
    }

    [Fact]
    public void Archive_MarksProductArchivedWithTimestamp()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);

        product.Archive(Now);

        Assert.True(product.IsArchived);
        Assert.Equal(Now, product.ArchivedAtUtc);
    }

    [Fact]
    public void Archive_RejectsAlreadyArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);
        product.Archive(Now);

        Assert.Throws<BusinessRuleException>(() => product.Archive(Now));
    }

    [Fact]
    public void Unarchive_RestoresActiveProduct()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);
        product.Archive(Now);

        product.Unarchive();

        Assert.False(product.IsArchived);
        Assert.Null(product.ArchivedAtUtc);
    }

    [Fact]
    public void Unarchive_RejectsActiveProduct()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);

        Assert.Throws<BusinessRuleException>(() => product.Unarchive());
    }

    [Fact]
    public void UpdateDetails_RejectsArchivedProduct()
    {
        var product = new Product("SKU-1", "Product", 1m, 5);
        product.Archive(Now);

        Assert.Throws<BusinessRuleException>(() => product.UpdateDetails("Changed", 2m));
    }
}
