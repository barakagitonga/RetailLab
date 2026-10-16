using Microsoft.EntityFrameworkCore;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests.Sync;

public sealed class ServerBackfillAndFeedReaderTests
{
    [Fact]
    public async Task Backfill_PreservesIdsPublishesOnceAndKeepsStableServerIdentity()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();

        await using (var legacyContext = database.CreateBaseContext())
        {
            legacyContext.Products.AddRange(
                new Product(firstId, "LEGACY-01", "First legacy product", 10m, 2),
                new Product(secondId, "LEGACY-02", "Second legacy product", 20m, 3));
            await legacyContext.SaveChangesAsync();
        }

        var service = new ServerBackfillService(database.Database);
        await service.PrepareAsync();
        var serverId = await service.GetServerInstanceIdAsync();
        await service.PrepareAsync();

        Assert.Equal(serverId, await service.GetServerInstanceIdAsync());

        await using var verification = database.CreateBaseContext();
        var products = await verification.Products.OrderBy(product => product.Sku).ToListAsync();
        Assert.Equal([firstId, secondId], products.Select(product => product.Id));
        Assert.All(products, product => Assert.Equal(1, product.ServerRevision));
        Assert.Equal(2, await verification.SyncChangeFeedEntries.CountAsync());
        Assert.Single(await verification.SyncServerProperties.ToListAsync());
    }

    [Fact]
    public async Task ConcurrentPreparation_ConvergesOnOneIdentityAndOneEntryPerProduct()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using (var legacyContext = database.CreateBaseContext())
        {
            legacyContext.Products.Add(new Product("RACE-BACKFILL", "Legacy", 4m, 4));
            await legacyContext.SaveChangesAsync();
        }

        var first = new ServerBackfillService(database.Database);
        var second = new ServerBackfillService(database.Database);
        await Task.WhenAll(first.PrepareAsync(), second.PrepareAsync());

        await using var verification = database.CreateBaseContext();
        Assert.Single(await verification.SyncServerProperties.ToListAsync());
        Assert.Single(await verification.SyncChangeFeedEntries.ToListAsync());
        Assert.Equal(1, (await verification.Products.SingleAsync()).ServerRevision);
    }

    [Fact]
    public async Task FeedReader_PagesInSequenceAndReportsHead()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using (var serverContext = database.CreateServerContext())
        {
            serverContext.Products.AddRange(
                new Product("PAGE-01", "First", 1m, 1),
                new Product("PAGE-02", "Second", 2m, 2),
                new Product("PAGE-03", "Third", 3m, 3));
            await serverContext.SaveChangesAsync();
        }

        await using var readContext = database.CreateBaseContext();
        var reader = new SyncFeedReader(readContext);

        var firstPage = await reader.ReadAfterAsync(0, 2);
        var secondPage = await reader.ReadAfterAsync(2, 2);

        Assert.Equal(3, firstPage.HeadSequence);
        Assert.True(firstPage.HasMore);
        Assert.Equal([1L, 2L], firstPage.Entries.Select(entry => entry.Sequence));
        Assert.False(secondPage.HasMore);
        Assert.Equal([3L], secondPage.Entries.Select(entry => entry.Sequence));
        Assert.All(firstPage.Entries, entry => Assert.Equal(entry.ProductId, entry.Snapshot.Id));
    }

    [Fact]
    public async Task FeedReader_EmptyFeedHasHeadZero()
    {
        await using var database = await TestServerDatabase.CreateAsync();
        await using var context = database.CreateBaseContext();

        var page = await new SyncFeedReader(context).ReadAfterAsync(0, 10);

        Assert.Equal(0, page.HeadSequence);
        Assert.Empty(page.Entries);
        Assert.False(page.HasMore);
    }
}
