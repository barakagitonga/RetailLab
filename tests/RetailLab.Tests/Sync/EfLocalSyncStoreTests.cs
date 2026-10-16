using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests.Sync;

public sealed class EfLocalSyncStoreTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 16, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MarkPairedAndApplyPage_PersistCheckpointAndProducts()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        var deviceId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var productId = Guid.NewGuid();

        await store.MarkPairedAsync(deviceId, serverId, Now);
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1)],
            expectedCursor: 0);
        await store.MarkSyncedAsync(Now.AddMinutes(1));

        var checkpoint = await store.GetCheckpointAsync();
        Assert.NotNull(checkpoint);
        Assert.Equal(deviceId, checkpoint.DeviceId);
        Assert.Equal(serverId, checkpoint.ServerInstanceId);
        Assert.Equal(1, checkpoint.LastPulledSequence);
        Assert.Equal(Now.AddMinutes(1), checkpoint.LastSyncAtUtc);

        await using var verification = database.Database.CreateDbContext();
        var product = await verification.Products.SingleAsync();
        Assert.Equal(productId, product.Id);
        Assert.Equal(1, product.ServerRevision);
    }

    [Fact]
    public async Task ApplyPage_WhenLaterEntryIsInvalid_RollsBackProductAndCursor()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        var entries = new[]
        {
            ProductServerSnapshotTests.CreateEntry(1, productId, 1),
            ProductServerSnapshotTests.CreateEntry(2, productId, 3, description: "Skipped revision")
        };

        await Assert.ThrowsAsync<BusinessRuleException>(
            () => store.ApplyPageAsync(entries, expectedCursor: 0));

        var checkpoint = await store.GetCheckpointAsync();
        Assert.NotNull(checkpoint);
        Assert.Equal(0, checkpoint.LastPulledSequence);
        await using var verification = database.Database.CreateDbContext();
        Assert.Empty(await verification.Products.ToListAsync());
    }

    [Fact]
    public async Task ApplyPage_WithPendingOutboxWork_StopsBeforeChangingProjection()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();

        await using (var context = database.Database.CreateDbContext())
        {
            context.SyncOutboxEntries.Add(new SyncOutboxEntry(
                Guid.NewGuid(),
                productId,
                SyncOperationKind.CreateProduct,
                "{}",
                null,
                null,
                Now));
            await context.SaveChangesAsync();
        }

        await Assert.ThrowsAsync<PendingLocalSyncWorkException>(
            () => store.ApplyPageAsync(
                [ProductServerSnapshotTests.CreateEntry(1, productId, 1)],
                expectedCursor: 0));

        var checkpoint = await store.GetCheckpointAsync();
        Assert.NotNull(checkpoint);
        Assert.Equal(0, checkpoint.LastPulledSequence);
    }

    [Fact]
    public async Task MarkPaired_WithExistingProduct_RejectsWithoutReplacingData()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        await using (var context = database.Database.CreateDbContext())
        {
            context.Products.Add(new Product("LOCAL-1", "Local product", 10m, 1));
            await context.SaveChangesAsync();
        }

        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        var exception = await Assert.ThrowsAsync<SyncEligibilityException>(
            () => store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now));

        Assert.Equal(1, exception.ProductCount);
        await using var verification = database.Database.CreateDbContext();
        Assert.Equal("LOCAL-1", (await verification.Products.SingleAsync()).Sku);
    }

    private sealed class TestClientDatabase : IAsyncDisposable
    {
        private TestClientDatabase(string directory, RetailLabSqliteDatabase database)
        {
            Directory = directory;
            Database = database;
        }

        private string Directory { get; }

        public RetailLabSqliteDatabase Database { get; }

        public static async Task<TestClientDatabase> CreateAsync()
        {
            var directory = Path.Combine(Path.GetTempPath(), $"RetailLab-tests-{Guid.NewGuid():N}");
            var database = new RetailLabSqliteDatabase(directory);
            await using var context = database.CreateDbContext();
            await context.Database.MigrateAsync();
            return new TestClientDatabase(directory, database);
        }

        public ValueTask DisposeAsync()
        {
            SqliteConnection.ClearAllPools();
            if (System.IO.Directory.Exists(Directory))
            {
                System.IO.Directory.Delete(Directory, recursive: true);
            }

            return ValueTask.CompletedTask;
        }
    }
}
