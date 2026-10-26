using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using RetailLab.Core;
using RetailLab.Data;
using RetailLab.Sync;

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

    [Fact]
    public async Task OfflineCapture_ChainsOperationsAndUploadResultsBecomeTerminalAtomically()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));

        var create = await service.CreateAsync("LOCAL-1", "First version", 10m, 2);
        var update = await service.UpdateAsync(create.ProductId, "Second version", 12m);

        var pending = await store.GetPendingOperationsAsync(limit: 25);
        Assert.Equal(2, pending.Count);
        Assert.Equal(create.OperationId, pending[0].Operation.OperationId);
        var chainedUpdate = Assert.IsType<UpdateProductDetailsOperation>(pending[1].Operation);
        Assert.Equal(create.OperationId, chainedUpdate.PredecessorOperationId);
        Assert.Null(chainedUpdate.BaseServerRevision);

        await store.ApplyUploadResultsAsync(
        [
            new UploadOperationResult(
                create.OperationId,
                SyncOperationOutcome.Accepted,
                null,
                1,
                null,
                false),
            new UploadOperationResult(
                update.OperationId,
                SyncOperationOutcome.Accepted,
                null,
                2,
                null,
                false)
        ]);

        await using var verification = database.Database.CreateDbContext();
        var product = await verification.Products.SingleAsync();
        Assert.Equal("Second version", product.Description);
        // The accepted revision arrives through the authoritative feed. Advancing
        // it here would make the client's own revision-1 feed entry look stale.
        Assert.Equal(0, product.ServerRevision);
        Assert.All(
            await verification.SyncOutboxEntries.OrderBy(entry => entry.LocalSequence).ToListAsync(),
            entry => Assert.Equal(OutboxProcessingState.Processed, entry.State));
    }

    [Fact]
    public async Task AdjustStockCapture_MutatesProjectionAndOutboxWithoutCreatingLedgerRow()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1, stock: 5)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));

        var operation = await service.AdjustStockAsync(
            productId,
            3,
            SynchronizedAdjustmentReasons.Restock);

        await using var verification = database.Database.CreateDbContext();
        Assert.Equal(8, (await verification.Products.SingleAsync()).StockQuantity);
        Assert.Empty(await verification.InventoryAdjustments.ToListAsync());
        var entry = await verification.SyncOutboxEntries.SingleAsync();
        Assert.Equal(operation.OperationId, entry.OperationId);
        Assert.Equal(SyncOperationKind.AdjustStock, entry.Kind);
        var roundTripped = Assert.IsType<AdjustStockOperation>(SyncOperationSerializer.Deserialize(entry));
        Assert.Equal(3, roundTripped.Delta);
        Assert.Equal(SynchronizedAdjustmentReasons.Restock, roundTripped.Reason);
    }

    [Fact]
    public async Task AdjustStockCapture_ArchivedProductWritesNeitherMutationNorOutbox()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(
                1,
                productId,
                1,
                stock: 5,
                isArchived: true,
                archivedAtUtc: Now)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.AdjustStockAsync(
            productId,
            2,
            SynchronizedAdjustmentReasons.Restock));

        await using var verification = database.Database.CreateDbContext();
        Assert.Equal(5, (await verification.Products.SingleAsync()).StockQuantity);
        Assert.Empty(await verification.SyncOutboxEntries.ToListAsync());
        Assert.Empty(await verification.InventoryAdjustments.ToListAsync());
    }

    [Fact]
    public async Task AcceptedAdjustment_CreatesPrescribedLedgerRowAndProcessesOutboxAtomically()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        var deviceId = Guid.NewGuid();
        await store.MarkPairedAsync(deviceId, Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1, stock: 5)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));
        var operation = await service.AdjustStockAsync(
            productId,
            3,
            SynchronizedAdjustmentReasons.Restock);
        var snapshot = new ProductSnapshot(
            productId,
            "SERVER-1",
            "Server product",
            10m,
            11,
            false,
            null,
            2);

        await store.ApplyUploadResultsAsync(
        [
            new UploadOperationResult(
                operation.OperationId,
                SyncOperationOutcome.Accepted,
                null,
                2,
                snapshot,
                false)
        ]);

        await using var verification = database.Database.CreateDbContext();
        var adjustment = await verification.InventoryAdjustments.SingleAsync();
        Assert.Equal(operation.OperationId, adjustment.Id);
        Assert.Equal(3, adjustment.QuantityChange);
        Assert.Equal(11, adjustment.ResultingQuantity);
        Assert.Equal($"sync-device:{deviceId:D}", adjustment.ActorIdentifier);
        Assert.Equal(Now, adjustment.CreatedAtUtc);
        Assert.Equal(
            OutboxProcessingState.Processed,
            (await verification.SyncOutboxEntries.SingleAsync()).State);
    }

    [Theory]
    [InlineData(SyncOperationOutcome.Conflict, "insufficient_stock")]
    [InlineData(SyncOperationOutcome.Rejected, "product_archived")]
    public async Task NonAcceptedAdjustment_CreatesNoLedgerRow(
        SyncOperationOutcome outcome,
        string code)
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        await store.MarkPairedAsync(Guid.NewGuid(), Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1, stock: 5)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));
        var operation = await service.AdjustStockAsync(
            productId,
            -2,
            SynchronizedAdjustmentReasons.Damage);

        await store.ApplyUploadResultsAsync(
        [
            new UploadOperationResult(operation.OperationId, outcome, code, null, null, false)
        ]);

        await using var verification = database.Database.CreateDbContext();
        Assert.Empty(await verification.InventoryAdjustments.ToListAsync());
        var expectedState = outcome == SyncOperationOutcome.Conflict
            ? OutboxProcessingState.Conflicted
            : OutboxProcessingState.Rejected;
        Assert.Equal(expectedState, (await verification.SyncOutboxEntries.SingleAsync()).State);
    }

    [Fact]
    public async Task AcceptedAdjustment_WithExactExistingLedgerRow_CompletesDefensiveRecovery()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        var deviceId = Guid.NewGuid();
        await store.MarkPairedAsync(deviceId, Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1, stock: 5)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));
        var operation = await service.AdjustStockAsync(
            productId,
            2,
            SynchronizedAdjustmentReasons.Restock);
        await using (var setup = database.Database.CreateDbContext())
        {
            var product = await setup.Products.SingleAsync();
            setup.InventoryAdjustments.Add(new InventoryAdjustment(
                operation.OperationId,
                product,
                operation.Delta,
                9,
                operation.Reason,
                $"sync-device:{deviceId:D}",
                operation.CreatedAtUtc));
            await setup.SaveChangesAsync();
        }
        var snapshot = new ProductSnapshot(
            productId, "SERVER-2", "Product", 10m, 9, false, null, 2);

        await store.ApplyUploadResultsAsync(
        [
            new UploadOperationResult(
                operation.OperationId,
                SyncOperationOutcome.Accepted,
                null,
                2,
                snapshot,
                false)
        ]);

        await using var verification = database.Database.CreateDbContext();
        Assert.Equal(1, await verification.InventoryAdjustments.CountAsync());
        Assert.Equal(
            OutboxProcessingState.Processed,
            (await verification.SyncOutboxEntries.SingleAsync()).State);
    }

    [Fact]
    public async Task AcceptedAdjustment_WithMismatchedExistingLedgerRow_RollsBackAndLeavesPending()
    {
        await using var database = await TestClientDatabase.CreateAsync();
        var store = new EfLocalSyncStore(database.Database.CreateDbContext);
        var deviceId = Guid.NewGuid();
        await store.MarkPairedAsync(deviceId, Guid.NewGuid(), Now);
        var productId = Guid.NewGuid();
        await store.ApplyPageAsync(
            [ProductServerSnapshotTests.CreateEntry(1, productId, 1, stock: 5)],
            expectedCursor: 0);
        var service = new OfflineProductOperationService(store, new TestTimeProvider(Now));
        var operation = await service.AdjustStockAsync(
            productId,
            2,
            SynchronizedAdjustmentReasons.Restock);
        await using (var setup = database.Database.CreateDbContext())
        {
            var product = await setup.Products.SingleAsync();
            setup.InventoryAdjustments.Add(new InventoryAdjustment(
                operation.OperationId,
                product,
                operation.Delta,
                8,
                operation.Reason,
                $"sync-device:{deviceId:D}",
                operation.CreatedAtUtc));
            await setup.SaveChangesAsync();
        }
        var snapshot = new ProductSnapshot(
            productId, "SERVER-3", "Product", 10m, 9, false, null, 2);

        await Assert.ThrowsAsync<SyncLocalStateException>(() => store.ApplyUploadResultsAsync(
        [
            new UploadOperationResult(
                operation.OperationId,
                SyncOperationOutcome.Accepted,
                null,
                2,
                snapshot,
                false)
        ]));

        await using var verification = database.Database.CreateDbContext();
        Assert.Equal(8, (await verification.InventoryAdjustments.SingleAsync()).ResultingQuantity);
        Assert.Equal(
            OutboxProcessingState.Pending,
            (await verification.SyncOutboxEntries.SingleAsync()).State);
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
