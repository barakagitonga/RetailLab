using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests.Sync;

public sealed class ServerFeedPublicationTests
{
    [Fact]
    public async Task ServerContext_PublishesCreateUpdateArchiveUnarchiveAndStockChanges()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateServerContext();

        var product = new Product("SYNC-01", "Initial description", 10m, 4);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        product.UpdateDetails("Updated description", 12m);
        await context.SaveChangesAsync();

        product.Archive(DateTimeOffset.UtcNow);
        await context.SaveChangesAsync();

        product.Unarchive();
        await context.SaveChangesAsync();

        product.AdjustStock(3);
        await context.SaveChangesAsync();

        var entries = await context.SyncChangeFeedEntries
            .AsNoTracking()
            .OrderBy(entry => entry.Sequence)
            .ToListAsync();

        Assert.Equal(5, product.ServerRevision);
        Assert.Equal(
            [
                ProductChangeKind.Created,
                ProductChangeKind.Updated,
                ProductChangeKind.Archived,
                ProductChangeKind.Unarchived,
                ProductChangeKind.Updated
            ],
            entries.Select(entry => entry.ChangeKind));
        Assert.Equal([1L, 2L, 3L, 4L, 5L], entries.Select(entry => entry.ServerRevision));
    }

    [Fact]
    public async Task ServerContext_PublishesMultipleProductsInOneSave()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateServerContext();

        var first = new Product("MULTI-01", "First", 1m, 1);
        var second = new Product("MULTI-02", "Second", 2m, 2);
        context.Products.AddRange(first, second);

        await context.SaveChangesAsync();

        Assert.Equal(1, first.ServerRevision);
        Assert.Equal(1, second.ServerRevision);
        Assert.Equal(2, await context.SyncChangeFeedEntries.CountAsync());
    }

    [Fact]
    public async Task FreshServerSeed_PublishesFiveRevisionOneCreatedEntries()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateServerContext();

        await DatabaseInitializer.SeedIfEmptyAsync(context);

        var products = await context.Products.OrderBy(product => product.Sku).ToListAsync();
        var entries = await context.SyncChangeFeedEntries
            .OrderBy(entry => entry.Sequence)
            .ToListAsync();
        Assert.Equal(5, products.Count);
        Assert.All(products, product => Assert.Equal(1, product.ServerRevision));
        Assert.Equal([1L, 2L, 3L, 4L, 5L], entries.Select(entry => entry.Sequence));
        Assert.All(entries, entry =>
        {
            Assert.Equal(1, entry.ServerRevision);
            Assert.Equal(ProductChangeKind.Created, entry.ChangeKind);
        });
    }

    [Fact]
    public async Task Checkout_PublishesStockSnapshotWithOrderInSameSave()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateServerContext();
        var product = new Product("CHECKOUT-SYNC", "Checkout product", 10m, 5);
        context.Products.Add(product);
        await context.SaveChangesAsync();

        var repository = new EfRetailRepository(context);
        await new BasketService(repository, TimeProvider.System)
            .AddAsync("customer-sync", product.Sku, 2);

        await new CheckoutService(repository, new OrderService(repository, TimeProvider.System))
            .CheckoutBasketAsync("customer-sync", Guid.NewGuid());

        var entries = await context.SyncChangeFeedEntries
            .OrderBy(entry => entry.Sequence)
            .ToListAsync();
        Assert.Equal(3, product.StockQuantity);
        Assert.Equal(2, product.ServerRevision);
        Assert.Equal(2, entries.Count);
        Assert.Equal(ProductChangeKind.Updated, entries[1].ChangeKind);
        Assert.Single(await context.Orders.ToListAsync());

        var pulled = await new SyncFeedReader(context).ReadAfterAsync(1, 10);
        Assert.Single(pulled.Entries);
        Assert.Equal(3, pulled.Entries[0].Snapshot.StockQuantity);
    }

    [Fact]
    public async Task BaseContext_DoesNotPublishOrAdvanceRevision()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateBaseContext();
        var product = new Product("LOCAL-01", "Local only", 1m, 1);
        context.Products.Add(product);

        await context.SaveChangesAsync();

        Assert.Equal(0, product.ServerRevision);
        Assert.Empty(await context.SyncChangeFeedEntries.ToListAsync());
    }

    [Fact]
    public async Task ServerContext_SynchronousSaveIsRejected()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateServerContext();
        context.Products.Add(new Product("SYNC-BLOCK", "Blocked sync save", 1m, 1));

        var exception = Assert.Throws<NotSupportedException>(() => context.SaveChanges());

        Assert.Contains("SaveChangesAsync", exception.Message);
    }

    [Fact]
    public async Task ConcurrencyFailure_RestoresRevisionAndRetryPublishesOnce()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using (var setup = database.CreateServerContext())
        {
            setup.Products.Add(new Product("RACE-01", "Original", 10m, 5));
            await setup.SaveChangesAsync();
        }

        await using var winnerContext = database.CreateServerContext();
        await using var loserContext = database.CreateServerContext();
        var winner = await winnerContext.Products.SingleAsync();
        var loser = await loserContext.Products.SingleAsync();
        winner.UpdateDetails("Winner", 11m);
        loser.UpdateDetails("Loser", 12m);

        await new EfRetailRepository(winnerContext).SaveProductChangesAsync();
        await Assert.ThrowsAsync<ProductConflictException>(
            () => new EfRetailRepository(loserContext).SaveProductChangesAsync());

        Assert.Equal(1, loser.ServerRevision);

        await using (var verification = database.CreateBaseContext())
        {
            Assert.Equal(2, await verification.SyncChangeFeedEntries.CountAsync());
        }

        await loserContext.Entry(loser).ReloadAsync();
        loser.UpdateDetails("Retry", 13m);
        await new EfRetailRepository(loserContext).SaveProductChangesAsync();

        await using var finalVerification = database.CreateBaseContext();
        var revisions = await finalVerification.SyncChangeFeedEntries
            .OrderBy(entry => entry.Sequence)
            .Select(entry => entry.ServerRevision)
            .ToListAsync();
        Assert.Equal([1L, 2L, 3L], revisions);
    }
}
