using RetailLab.Core;

namespace RetailLab.Tests;

internal sealed class InMemoryRetailRepository(params Product[] products) : IRetailRepository
{
    public List<Product> Products { get; } = [.. products];

    public List<Bookmark> Bookmarks { get; } = [];

    public List<Order> Orders { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Product>>(Products.OrderBy(product => product.Sku).ToList());
    }

    public Task<Product?> FindProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Products.SingleOrDefault(
                product => string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase)));
    }

    public Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Bookmark>>(
            Bookmarks.Where(bookmark => bookmark.CustomerIdentifier == customerIdentifier).ToList());
    }

    public Task<Bookmark?> FindBookmarkAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Bookmarks.SingleOrDefault(
                bookmark => bookmark.CustomerIdentifier == customerIdentifier &&
                            bookmark.ProductId == productId));
    }

    public void AddBookmark(Bookmark bookmark) => Bookmarks.Add(bookmark);

    public void RemoveBookmark(Bookmark bookmark) => Bookmarks.Remove(bookmark);

    public void AddOrder(Order order) => Orders.Add(order);

    public Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            Orders.Where(order => order.CustomerIdentifier == customerIdentifier).ToList());
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
