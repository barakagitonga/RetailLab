using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public sealed class EfRetailRepository(RetailLabDbContext dbContext) : IRetailRepository
{
    // SQLite extended result codes (https://www.sqlite.org/rescode.html), named
    // so the conflict translation reads without magic numbers.
    private const int SqliteConstraintPrimaryKey = 1555; // SQLITE_CONSTRAINT_PRIMARYKEY
    private const int SqliteConstraintUnique = 2067; // SQLITE_CONSTRAINT_UNIQUE

    public async Task<IReadOnlyList<Product>> GetProductsAsync(
        bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Products.AsNoTracking();

        if (!includeArchived)
        {
            query = query.Where(product => !product.IsArchived);
        }

        return await query
            .OrderBy(product => product.Sku)
            .ToListAsync(cancellationToken);
    }

    public Task<Product?> FindProductBySkuAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Products.SingleOrDefaultAsync(
            product => product.Sku == sku,
            cancellationToken);
    }

    public void AddProduct(Product product)
    {
        dbContext.Products.Add(product);
    }

    public void AddInventoryAdjustment(InventoryAdjustment adjustment)
    {
        dbContext.InventoryAdjustments.Add(adjustment);
    }

    public async Task<IReadOnlyList<InventoryAdjustment>> GetAdjustmentsAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        var adjustments = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .Where(adjustment => adjustment.ProductId == productId)
            .ToListAsync(cancellationToken);

        // SQLite cannot translate DateTimeOffset ordering. The product filter still
        // runs in the database; this small prototype sorts the resulting history here.
        return adjustments
            .OrderBy(adjustment => adjustment.CreatedAtUtc)
            .ThenBy(adjustment => adjustment.Id)
            .ToList();
    }

    public async Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookmarks
            .AsNoTracking()
            .Include(bookmark => bookmark.Product)
            .Where(bookmark => bookmark.CustomerIdentifier == customerIdentifier &&
                               !bookmark.Product.IsArchived)
            .OrderBy(bookmark => bookmark.Product.Sku)
            .ToListAsync(cancellationToken);
    }

    public Task<Bookmark?> FindBookmarkAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.Bookmarks.SingleOrDefaultAsync(
            bookmark => bookmark.CustomerIdentifier == customerIdentifier &&
                        bookmark.ProductId == productId,
            cancellationToken);
    }

    public void AddBookmark(Bookmark bookmark)
    {
        dbContext.Bookmarks.Add(bookmark);
    }

    public void RemoveBookmark(Bookmark bookmark)
    {
        dbContext.Bookmarks.Remove(bookmark);
    }

    public async Task<IReadOnlyList<BasketItem>> GetBasketItemsAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        // One product-including query, deliberately without an archived-product
        // filter: archived products stay in the basket so the customer can see
        // and remove them.
        return await dbContext.BasketItems
            .AsNoTracking()
            .Include(item => item.Product)
            .Where(item => item.CustomerIdentifier == customerIdentifier)
            .OrderBy(item => item.Product.Sku)
            .ToListAsync(cancellationToken);
    }

    public Task<BasketItem?> FindBasketItemAsync(
        string customerIdentifier,
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        return dbContext.BasketItems.SingleOrDefaultAsync(
            item => item.CustomerIdentifier == customerIdentifier &&
                   item.ProductId == productId,
            cancellationToken);
    }

    public void AddBasketItem(BasketItem item)
    {
        dbContext.BasketItems.Add(item);
    }

    public void RemoveBasketItem(BasketItem item)
    {
        dbContext.BasketItems.Remove(item);
    }

    public async Task<IReadOnlyList<BasketItem>> GetBasketItemsForCheckoutAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        // Tracked by design: checkout deletes these rows, and each delete must
        // carry the original Version so a concurrent basket change fails the
        // save instead of clearing lines the customer no longer recognizes.
        return await dbContext.BasketItems
            .Include(item => item.Product)
            .Where(item => item.CustomerIdentifier == customerIdentifier)
            .OrderBy(item => item.Product.Sku)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveBasketChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A stale Version lost the optimistic-concurrency check: another
            // request changed the basket first, so the customer retries
            // against the current basket.
            throw new BasketConflictException(exception);
        }
        catch (DbUpdateException exception) when (IsConcurrentBasketInsert(exception))
        {
            throw new BasketConflictException(exception);
        }
    }

    /// <summary>
    /// True only for the expected race: two requests simultaneously adding the
    /// same basket line collide on its key. Anything else (check violations,
    /// foreign keys, unrelated entities) skips translation and propagates to
    /// the normal unexpected-error handling.
    /// </summary>
    private static bool IsConcurrentBasketInsert(DbUpdateException exception) =>
        IsKeyCollision(exception.InnerException) && HasOnlyAddedBasketItems(exception);

    private static bool IsKeyCollision(Exception? innerException) =>
        innerException is SqliteException sqlite &&
        sqlite.SqliteExtendedErrorCode is SqliteConstraintPrimaryKey or SqliteConstraintUnique;

    private static bool HasOnlyAddedBasketItems(DbUpdateException exception) =>
        exception.Entries.Count > 0 &&
        exception.Entries.All(entry =>
            entry.State == EntityState.Added && entry.Entity is BasketItem);

    public void AddOrder(Order order)
    {
        dbContext.Orders.Add(order);
    }

    public async Task<IReadOnlyList<Order>> GetOrdersAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
            .Where(order => order.CustomerIdentifier == customerIdentifier)
            .ToListAsync(cancellationToken);

        // SQLite cannot translate DateTimeOffset ordering. The customer filter still
        // runs in the database; this small prototype sorts the resulting history here.
        return orders.OrderByDescending(order => order.PlacedAtUtc).ToList();
    }

    public Task<Order?> FindOrderAsync(
        string customerIdentifier,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        // Untracked by design: idempotency checks and the post-conflict
        // recovery re-read must see committed database state, never the
        // rolled-back entities tracked by a failed save.
        return dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
            .SingleOrDefaultAsync(
                order => order.CustomerIdentifier == customerIdentifier &&
                         order.Id == orderId,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task SaveProductChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A stale product Version lost the optimistic-concurrency check:
            // another request changed the product first, so staff and console
            // callers retry against the current product details.
            throw new ProductConflictException(exception);
        }
    }

    public async Task SaveCheckoutChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            // A stale product or basket Version lost its check: another
            // request changed the data first, so the whole checkout (order,
            // stock, audit records, basket removal) rolled back and the
            // customer retries against current state.
            throw new CheckoutConflictException(exception);
        }
        catch (DbUpdateException exception) when (IsDuplicateOrderInsert(exception))
        {
            throw new CheckoutConflictException(exception);
        }
    }

    /// <summary>
    /// True only for the expected duplicate-submission race: two requests
    /// inserting the same order id collide on its key. Anything else (check
    /// violations, foreign keys, unrelated entities) skips translation and
    /// propagates to the normal unexpected-error handling.
    /// </summary>
    private static bool IsDuplicateOrderInsert(DbUpdateException exception) =>
        IsKeyCollision(exception.InnerException) && HasAddedOrder(exception);

    private static bool HasAddedOrder(DbUpdateException exception) =>
        exception.Entries.Count > 0 &&
        exception.Entries.Any(entry =>
            entry.State == EntityState.Added && entry.Entity is Order);
}
