using RetailLab.Core;
using RetailLab.Web.Services;

namespace RetailLab.Tests;

public sealed class BasketDisplayTests
{
    private readonly CatalogDisplayMapper _mapper = new();

    [Fact]
    public void ToBasketLine_MapsPricesTotalsAndAvailability()
    {
        var product = new Product("AUR-100", "Aurora insulated travel mug", 24.95m, 18);
        var item = new BasketItem(
            "customer-1",
            product,
            2,
            new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

        var line = _mapper.ToBasketLine(item);

        Assert.Equal("AUR-100", line.Sku);
        Assert.Equal("Aurora insulated travel mug", line.Description);
        Assert.Equal("$24.95", line.PriceDisplay);
        Assert.Equal(2, line.Quantity);
        Assert.Equal("$49.90", line.LineTotalDisplay);
        Assert.Equal("In stock", line.Availability);
        Assert.False(line.IsArchived);
    }

    [Fact]
    public void ToBasketLine_KeepsOutOfStockBandForRetainedLine()
    {
        var product = new Product("OOS-1", "Gone product", 9.99m, 0);
        var item = new BasketItem(
            "customer-1",
            product,
            1,
            new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

        var line = _mapper.ToBasketLine(item);

        Assert.Equal("Out of stock", line.Availability);
        Assert.Equal("low", line.AvailabilityTone);
        Assert.False(line.IsArchived);
    }

    [Fact]
    public void ToBasketLine_FlagsArchivedProduct()
    {
        var product = new Product("OLD-1", "Retired product", 5.00m, 9);
        product.Archive(new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));
        var item = new BasketItem(
            "customer-1",
            product,
            1,
            new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero));

        var line = _mapper.ToBasketLine(item);

        Assert.True(line.IsArchived);
    }
}
