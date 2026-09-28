using RetailLab.Core;
using RetailLab.Web.Services;

namespace RetailLab.Tests;

public sealed class CatalogDisplayTests
{
    private readonly CatalogDisplayMapper _mapper = new();

    [Fact]
    public void FormatPrice_UsesUsDollars()
    {
        Assert.Equal("$24.95", CatalogDisplayMapper.FormatPrice(24.95m));
        Assert.Equal("$0.00", CatalogDisplayMapper.FormatPrice(0m));
    }

    [Theory]
    [InlineData(0, "Out of stock")]
    [InlineData(1, "Low stock")]
    [InlineData(5, "Low stock")]
    [InlineData(6, "In stock")]
    [InlineData(40, "In stock")]
    public void AvailabilityFor_MapsBands(int stockQuantity, string expected)
    {
        Assert.Equal(expected, CatalogDisplayMapper.AvailabilityFor(stockQuantity));
    }

    [Fact]
    public void ToSummary_MapsAllDisplayFields()
    {
        var product = new Product("AUR-100", "Aurora insulated travel mug", 24.95m, 18);

        var summary = _mapper.ToSummary(product);

        Assert.Equal("AUR-100", summary.Sku);
        Assert.Equal("Aurora insulated travel mug", summary.Description);
        Assert.Equal("$24.95", summary.PriceDisplay);
        Assert.Equal("In stock", summary.Availability);
        Assert.False(summary.IsOutOfStock);
    }

    [Fact]
    public void ToDetails_MarksOutOfStockProduct()
    {
        var product = new Product("OOS-1", "Gone product", 9.99m, 0);

        var details = _mapper.ToDetails(product);

        Assert.Equal("Out of stock", details.Availability);
        Assert.True(details.IsOutOfStock);
    }
}
