namespace RetailLab.Core;

public interface IRetailRepository
{
    Task<IReadOnlyList<Product>> GetProductsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default);

    Task<Product?> FindProductBySkuAsync(string sku, CancellationToken cancellationToken = default);

    void AddProduct(Product product);

    void AddInventoryAdjustment(InventoryAdjustment adjustment);

    Task<IReadOnlyList<InventoryAdjustment>> GetAdjustmentsAsync(
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

    Task<Bookmark?> FindBookmarkAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default);

    void AddBookmark(Bookmark bookmark);

    void RemoveBookmark(Bookmark bookmark);

    Task<IReadOnlyList<BasketItem>> GetBasketItemsAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

    Task<BasketItem?> FindBasketItemAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default);

    void AddBasketItem(BasketItem item);

    void RemoveBasketItem(BasketItem item);

    /// <summary>
    /// Persists tracked basket changes. Translates expected persistence
    /// conflicts (key collisions, optimistic-concurrency losses) into the
    /// Core-owned <see cref="BasketConflictException" /> so callers never see
    /// storage-specific exception types.
    /// </summary>
    Task SaveBasketChangesAsync(CancellationToken cancellationToken = default);

    void AddOrder(Order order);

    Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
