using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class BookmarkServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 24, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task AddAsync_AddsBookmarkForCustomerAndProduct()
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BookmarkService(repository, new TestTimeProvider(Now));

        await service.AddAsync("customer-1", "sku-1");

        var bookmark = Assert.Single(repository.Bookmarks);
        Assert.Equal("customer-1", bookmark.CustomerIdentifier);
        Assert.Equal(product.Id, bookmark.ProductId);
        Assert.Equal(Now, bookmark.CreatedAtUtc);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task AddAsync_RejectsDuplicateBookmark()
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BookmarkService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1");

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.AddAsync("customer-1", "SKU-1"));

        Assert.Single(repository.Bookmarks);
        Assert.Equal(1, repository.SaveCount);
    }

    [Fact]
    public async Task RemoveAsync_RemovesExistingBookmark()
    {
        var product = new Product("SKU-1", "Product", 10m, 2);
        var repository = new InMemoryRetailRepository(product);
        var service = new BookmarkService(repository, new TestTimeProvider(Now));
        await service.AddAsync("customer-1", "SKU-1");

        await service.RemoveAsync("customer-1", "SKU-1");

        Assert.Empty(repository.Bookmarks);
        Assert.Equal(2, repository.SaveCount);
    }
}
