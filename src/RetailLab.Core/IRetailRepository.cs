namespace RetailLab.Core;

public interface IRetailRepository
{
    Task<IReadOnlyList<Product>> GetProductsAsync(CancellationToken cancellationToken = default);

    Task<Product?> FindProductBySkuAsync(string sku, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

    Task<Bookmark?> FindBookmarkAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default);

    void AddBookmark(Bookmark bookmark);

    void RemoveBookmark(Bookmark bookmark);

    void AddOrder(Order order);

    Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
