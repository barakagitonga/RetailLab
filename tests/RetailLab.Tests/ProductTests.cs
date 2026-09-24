using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class ProductTests
{
    [Fact]
    public void Constructor_TrimsValuesAndStoresValidData()
    {
        var product = new Product(" SKU-1 ", " Sample product ", 12.50m, 5);

        Assert.Equal("SKU-1", product.Sku);
        Assert.Equal("Sample product", product.Description);
        Assert.Equal(12.50m, product.Price);
        Assert.Equal(5, product.StockQuantity);
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
}
