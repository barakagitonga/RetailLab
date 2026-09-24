using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class OrderServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PlaceAsync_CreatesSnapshotAndReducesStock()
    {
        var product = new Product("SKU-1", "Original description", 12.50m, 5);
        var repository = new InMemoryRetailRepository(product);
        var service = new OrderService(repository, new TestTimeProvider(Now));

        var order = await service.PlaceAsync(
            "customer-1",
            [new OrderItemRequest("SKU-1", 2)]);

        var line = Assert.Single(order.Lines);
        Assert.Equal("Original description", line.ProductDescription);
        Assert.Equal(12.50m, line.UnitPrice);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(25.00m, order.Total);
        Assert.Equal(3, product.StockQuantity);
        Assert.Equal(Now, order.PlacedAtUtc);
        Assert.Single(repository.Orders);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task PlaceAsync_CombinesRepeatedSkuLines()
    {
        var product = new Product("SKU-1", "Product", 5m, 10);
        var repository = new InMemoryRetailRepository(product);
        var service = new OrderService(repository, new TestTimeProvider(Now));

        var order = await service.PlaceAsync(
            "customer-1",
            [new OrderItemRequest("sku-1", 2), new OrderItemRequest("SKU-1", 3)]);

        var line = Assert.Single(order.Lines);
        Assert.Equal(5, line.Quantity);
        Assert.Equal(5, product.StockQuantity);
    }

    [Fact]
    public async Task PlaceAsync_RejectsInsufficientStockWithoutChangingAnyProduct()
    {
        var available = new Product("SKU-1", "Available product", 5m, 10);
        var insufficient = new Product("SKU-2", "Low-stock product", 7m, 1);
        var repository = new InMemoryRetailRepository(available, insufficient);
        var service = new OrderService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PlaceAsync(
                "customer-1",
                [new OrderItemRequest("SKU-1", 2), new OrderItemRequest("SKU-2", 2)]));

        Assert.Equal(10, available.StockQuantity);
        Assert.Equal(1, insufficient.StockQuantity);
        Assert.Empty(repository.Orders);
        Assert.Equal(0, repository.SaveCount);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task PlaceAsync_RejectsNonPositiveQuantity(int quantity)
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 10));
        var service = new OrderService(repository, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.PlaceAsync(
                "customer-1",
                [new OrderItemRequest("SKU-1", quantity)]));
    }
}
