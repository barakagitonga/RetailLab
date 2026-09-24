using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

public sealed class EfRetailRepository(RetailLabDbContext dbContext) : IRetailRepository
{
    public async Task<IReadOnlyList<Product>> GetProductsAsync(
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Products
            .AsNoTracking()
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

    public async Task<IReadOnlyList<Bookmark>> GetBookmarksAsync(
        string customerIdentifier,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Bookmarks
            .AsNoTracking()
            .Include(bookmark => bookmark.Product)
            .Where(bookmark => bookmark.CustomerIdentifier == customerIdentifier)
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

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
