using RetailLab.Core;
using RetailLab.Web.Services;

namespace RetailLab.Tests;

public sealed class OrderDisplayTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    private readonly CatalogDisplayMapper _mapper = new();

    [Fact]
    public async Task ToOrderSummary_MapsPlacedAtItemsAndTotal()
    {
        var repository = new InMemoryRetailRepository(
            new Product("SKU-1", "First product", 12.50m, 10),
            new Product("SKU-2", "Second product", 7.00m, 10));
        var order = await new OrderService(repository, new TestTimeProvider(Now))
            .PlaceAsync(
                "customer-1",
                [new OrderItemRequest("SKU-1", 2), new OrderItemRequest("SKU-2", 1)]);

        var summary = _mapper.ToOrderSummary(order);

        Assert.Equal(order.Id, summary.Id);
        Assert.Equal("September 24, 2026 10:00 AM UTC", summary.PlacedAtDisplay);
        Assert.Equal("2 items", summary.ItemsDisplay);
        Assert.Equal("$32.00", summary.TotalDisplay);
    }

    [Fact]
    public async Task ToOrderSummary_UsesSingularItem()
    {
        var repository = new InMemoryRetailRepository(
            new Product("SKU-1", "Product", 9.99m, 10));
        var order = await new OrderService(repository, new TestTimeProvider(Now))
            .PlaceAsync("customer-1", [new OrderItemRequest("SKU-1", 1)]);

        var summary = _mapper.ToOrderSummary(order);

        Assert.Equal("1 item", summary.ItemsDisplay);
        Assert.Equal("$9.99", summary.TotalDisplay);
    }

    [Fact]
    public async Task ToOrderDetails_MapsSnapshotLinesOrderedBySku()
    {
        var repository = new InMemoryRetailRepository(
            new Product("SKU-1", "First product", 12.50m, 10),
            new Product("SKU-2", "Second product", 7.00m, 10));
        var order = await new OrderService(repository, new TestTimeProvider(Now))
            .PlaceAsync(
                "customer-1",
                [new OrderItemRequest("SKU-2", 1), new OrderItemRequest("SKU-1", 2)]);

        var details = _mapper.ToOrderDetails(order);

        Assert.Equal(order.Id, details.Id);
        Assert.Equal("September 24, 2026 10:00 AM UTC", details.PlacedAtDisplay);
        Assert.Equal("$32.00", details.TotalDisplay);
        Assert.Equal(2, details.Lines.Count);
        Assert.Equal("SKU-1", details.Lines[0].Sku);
        Assert.Equal("First product", details.Lines[0].Description);
        Assert.Equal(2, details.Lines[0].Quantity);
        Assert.Equal("$12.50", details.Lines[0].UnitPriceDisplay);
        Assert.Equal("$25.00", details.Lines[0].LineTotalDisplay);
        Assert.Equal("SKU-2", details.Lines[1].Sku);
    }
}
