using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class CatalogServiceTests
{
    [Fact]
    public async Task GetActiveProductsAsync_ExcludesArchivedProducts()
    {
        var archived = new Product("OLD-1", "Old product", 5m, 2);
        archived.Archive(new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryRetailRepository(
            new Product("NEW-1", "New product", 5m, 2),
            archived);
        var service = new CatalogService(repository);

        var active = await service.GetActiveProductsAsync();

        var single = Assert.Single(active);
        Assert.Equal("NEW-1", single.Sku);
    }

    [Fact]
    public async Task FindActiveProductAsync_ReturnsProductRegardlessOfSkuCase()
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 2));
        var service = new CatalogService(repository);

        var product = await service.FindActiveProductAsync("sku-1");

        Assert.NotNull(product);
        Assert.Equal("SKU-1", product.Sku);
    }

    [Fact]
    public async Task FindActiveProductAsync_ReturnsNullForArchivedProduct()
    {
        var archived = new Product("OLD-1", "Old product", 5m, 2);
        archived.Archive(new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero));
        var repository = new InMemoryRetailRepository(archived);
        var service = new CatalogService(repository);

        Assert.Null(await service.FindActiveProductAsync("OLD-1"));
    }

    [Theory]
    [InlineData("MISSING")]
    [InlineData("")]
    [InlineData("   ")]
    public async Task FindActiveProductAsync_ReturnsNullForUnknownOrBlankSku(string sku)
    {
        var repository = new InMemoryRetailRepository(new Product("SKU-1", "Product", 5m, 2));
        var service = new CatalogService(repository);

        Assert.Null(await service.FindActiveProductAsync(sku));
    }
}
