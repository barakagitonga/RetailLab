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
    /// Loads the customer's basket lines with products for checkout. The
    /// returned items are tracked so their removal carries the
    /// optimistic-concurrency check.
    /// </summary>
    Task<IReadOnlyList<BasketItem>> GetBasketItemsForCheckoutAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default);

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

    /// <summary>
    /// Finds one order by id within one customer's history. Returns null when
    /// the id is unknown or belongs to another customer. Reads committed
    /// state without tracking so idempotency checks see the database, never a
    /// failed save's tracked entities.
    /// </summary>
    Task<Order?> FindOrderAsync(
        string customerIdentifier,
        Guid orderId,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists tracked product changes (staff edits, stock adjustments,
    /// console orders). Translates stale product versions into the Core-owned
    /// <see cref="ProductConflictException" /> so callers never see
    /// storage-specific exception types.
    /// </summary>
    Task SaveProductChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Persists tracked checkout changes (order, stock, audit records, basket
    /// removal) in one save. Translates expected persistence conflicts (stale
    /// product or basket versions, duplicate order inserts) into the
    /// Core-owned <see cref="CheckoutConflictException" /> so callers never
    /// see storage-specific exception types.
    /// </summary>
    Task SaveCheckoutChangesAsync(CancellationToken cancellationToken = default);
}
