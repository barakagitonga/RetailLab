using RetailLab.Core;

namespace RetailLab.Tests;

internal sealed class InMemoryRetailRepository(params Product[] products) : IRetailRepository
{
    public List<Product> Products { get; } = [.. products];

    public List<Bookmark> Bookmarks { get; } = [];

    public List<BasketItem> BasketItems { get; } = [];

    public List<Order> Orders { get; } = [];

    public List<InventoryAdjustment> Adjustments { get; } = [];

    public int SaveCount { get; private set; }

    public Task<IReadOnlyList<Product>> GetProductsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Product>>(
            Products
                .Where(product => includeArchived || !product.IsArchived)
                .OrderBy(product => product.Sku)
                .ToList());
    }

    public Task<Product?> FindProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Products.SingleOrDefault(
                product => string.Equals(product.Sku, sku, StringComparison.OrdinalIgnoreCase)));
    }

    public void AddProduct(Product product) => Products.Add(product);

    public void AddInventoryAdjustment(InventoryAdjustment adjustment) => Adjustments.Add(adjustment);

    public Task<IReadOnlyList<InventoryAdjustment>> GetAdjustmentsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<InventoryAdjustment>>(
            Adjustments
                .Where(adjustment => adjustment.ProductId == productId)
                .OrderBy(adjustment => adjustment.CreatedAtUtc)
                .ToList());
    }

    public Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Bookmark>>(
            Bookmarks
                .Where(bookmark => bookmark.CustomerIdentifier == customerIdentifier &&
                                   !bookmark.Product.IsArchived)
                .ToList());
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

    public Task<IReadOnlyList<BasketItem>> GetBasketItemsAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        // Mirrors the EF query: product-including, ordered by SKU, with no
        // archived-product filter so retained archived lines stay visible.
        return Task.FromResult<IReadOnlyList<BasketItem>>(
            BasketItems
                .Where(item => item.CustomerIdentifier == customerIdentifier)
                .OrderBy(item => item.Product.Sku)
                .ToList());
    }

    public Task<BasketItem?> FindBasketItemAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            BasketItems.SingleOrDefault(
                item => item.CustomerIdentifier == customerIdentifier &&
                        item.ProductId == productId));
    }

    public void AddBasketItem(BasketItem item) => BasketItems.Add(item);

    public void RemoveBasketItem(BasketItem item) => BasketItems.Remove(item);

    public Task<IReadOnlyList<BasketItem>> GetBasketItemsForCheckoutAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        // Same live references as the display query. The in-memory double
        // enforces no optimistic concurrency, so checkout races are covered
        // by the SQLite integration tests instead.
        return GetBasketItemsAsync(customerIdentifier, cancellationToken);
    }

    public Task SaveBasketChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public void AddOrder(Order order) => Orders.Add(order);

    public Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IReadOnlyList<Order>>(
            Orders.Where(order => order.CustomerIdentifier == customerIdentifier).ToList());
    }

    public Task<Order?> FindOrderAsync(
        string customerIdentifier,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            Orders.SingleOrDefault(
                order => order.CustomerIdentifier == customerIdentifier &&
                         order.Id == orderId));
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SaveProductChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }

    public Task SaveCheckoutChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}
