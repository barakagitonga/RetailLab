using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using RetailLab.Core;
using RetailLab.Data;

namespace RetailLab.Tests;

public sealed class SyncPersistenceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 15, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task MigrationUpgrade_FromProductVersion_PreservesExistingDataAndSetsServerRevisionZero()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var existingProductId = Guid.NewGuid();
        var existingOrderLineId = Guid.NewGuid();

        // 1. Migrate only up to the previous migration: "ProductVersion"
        await using (var preMigrationContext = new RetailLabDbContext(options))
        {
            var migrator = preMigrationContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("ProductVersion");

            // Insert existing rows using raw SQL matching the schema at ProductVersion
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Products (Id, Sku, Description, Price, StockQuantity, Version, IsArchived, ArchivedAtUtc)
                VALUES ($id, 'LEGACY-01', 'Legacy product from previous migration', 19.95, 15, 2, 0, NULL);

                INSERT INTO Orders (Id, CustomerIdentifier, PlacedAtUtc)
                VALUES ($orderId, 'cust-legacy-1', '2026-09-29 12:00:00');

                INSERT INTO OrderLines (Id, OrderId, ProductId, Sku, ProductDescription, UnitPrice, Quantity)
                VALUES ($lineId, $orderId, $id, 'LEGACY-01', 'Legacy product', 19.95, 1);
            ";
            cmd.Parameters.AddWithValue("$id", existingProductId.ToString().ToUpperInvariant());
            var orderId = Guid.NewGuid();
            cmd.Parameters.AddWithValue("$orderId", orderId.ToString().ToUpperInvariant());
            cmd.Parameters.AddWithValue("$lineId", existingOrderLineId.ToString().ToUpperInvariant());
            await cmd.ExecuteNonQueryAsync();
        }

        // 2. Apply all pending migrations (including SynchronizationFoundations)
        await using (var upgradeContext = new RetailLabDbContext(options))
        {
            await upgradeContext.Database.MigrateAsync();
        }

        // 3. Verify in upgraded context: existing product row is preserved and has ServerRevision = 0
        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var product = await verificationContext.Products.FindAsync(existingProductId);
            Assert.NotNull(product);
            Assert.Equal("LEGACY-01", product.Sku);
            Assert.Equal(19.95m, product.Price);
            Assert.Equal(15, product.StockQuantity);
            Assert.Equal(2, product.Version);
            Assert.Equal(0L, product.ServerRevision); // Safely defaulted to 0

            var order = await verificationContext.Orders.Include(o => o.Lines).FirstOrDefaultAsync();
            Assert.NotNull(order);
            Assert.Equal("cust-legacy-1", order.CustomerIdentifier);
            Assert.Single(order.Lines);
        }
    }

    [Fact]
    public async Task SyncClientState_PersistsAndCursorRoundTrips()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var deviceId = Guid.NewGuid();

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var clientState = new SyncClientState(deviceId);
            clientState.Pair("server-store-central", Now, initialSequence: 42L);
            clientState.RecordSuccessfulSync(Now.AddMinutes(5), lastPulledSequence: 45L);

            context.SyncClientStates.Add(clientState);
            await context.SaveChangesAsync();
        }

        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var loaded = await verificationContext.SyncClientStates.FindAsync(SyncClientState.SingletonId);
            Assert.NotNull(loaded);
            Assert.Equal(deviceId, loaded.DeviceId);
            Assert.Equal("server-store-central", loaded.PairedServerId);
            Assert.Equal(45L, loaded.LastPulledSequence);
            Assert.Equal(Now.ToUniversalTime(), loaded.PairedAtUtc);
            Assert.Equal(Now.AddMinutes(5).ToUniversalTime(), loaded.LastSyncAtUtc);
        }
    }

    [Fact]
    public async Task SyncClientState_EnforcesSingletonConstraint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var state1 = new SyncClientState(Guid.NewGuid());
            context.SyncClientStates.Add(state1);
            await context.SaveChangesAsync();

            // Attempting to insert a second row with a different ID will violate the CK_SyncClientStates_Singleton check constraint
            await using var cmd = connection.CreateCommand();
            cmd.CommandText = "INSERT INTO SyncClientStates (Id, DeviceId, LastPulledSequence) VALUES (2, '00000000-0000-0000-0000-000000000001', 0);";

            var ex = await Assert.ThrowsAsync<SqliteException>(() => cmd.ExecuteNonQueryAsync());
            Assert.Contains("CHECK constraint failed", ex.Message);
        }
    }

    [Fact]
    public async Task SyncOutboxEntries_LocalSequencesAreMonotonic_AndOperationIdsAreUnique()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var op1Id = Guid.NewGuid();
        var op2Id = Guid.NewGuid();
        var prodId = Guid.NewGuid();

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var entry1 = new SyncOutboxEntry(
                op1Id,
                prodId,
                SyncOperationKind.CreateProduct,
                "{\"sku\":\"SKU-1\"}",
                baseServerRevision: null,
                predecessorOperationId: null,
                Now);

            var entry2 = new SyncOutboxEntry(
                op2Id,
                prodId,
                SyncOperationKind.UpdateProductDetails,
                "{\"desc\":\"Revised\"}",
                baseServerRevision: null,
                predecessorOperationId: op1Id,
                Now.AddSeconds(1));

            context.SyncOutboxEntries.AddRange(entry1, entry2);
            await context.SaveChangesAsync();

            Assert.True(entry1.LocalSequence > 0);
            Assert.True(entry2.LocalSequence > entry1.LocalSequence);
        }

        // Proving unique constraint on OperationId
        await using (var context = new RetailLabDbContext(options))
        {
            var duplicateEntry = new SyncOutboxEntry(
                op1Id, // same OperationId
                prodId,
                SyncOperationKind.ArchiveProduct,
                "{}",
                baseServerRevision: 1L,
                predecessorOperationId: null,
                Now.AddSeconds(2));

            context.SyncOutboxEntries.Add(duplicateEntry);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
            Assert.IsType<SqliteException>(ex.InnerException);
            Assert.Contains("UNIQUE constraint failed", ex.InnerException!.Message);
        }
    }

    [Fact]
    public async Task SyncInboxReceipts_EnforcesOneTerminalReceiptPerOperationId()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var opId = Guid.NewGuid();

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var receipt = new SyncInboxReceipt(
                opId,
                SyncOperationOutcome.Accepted,
                "{\"revision\":5}",
                Now);

            context.SyncInboxReceipts.Add(receipt);
            await context.SaveChangesAsync();
        }

        // Duplicate insert with same OperationId must fail
        await using (var duplicateContext = new RetailLabDbContext(options))
        {
            var duplicateReceipt = new SyncInboxReceipt(
                opId,
                SyncOperationOutcome.Accepted,
                "{\"revision\":5}",
                Now.AddSeconds(10));

            duplicateContext.SyncInboxReceipts.Add(duplicateReceipt);
            var ex = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Assert.IsType<SqliteException>(ex.InnerException);
            Assert.Contains("UNIQUE constraint failed", ex.InnerException!.Message);
        }
    }

    [Fact]
    public async Task SyncChangeFeedEntries_SequencesAreMonotonic()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var prodId = Guid.NewGuid();

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var feed1 = new SyncChangeFeedEntry(
                prodId,
                ProductChangeKind.Created,
                serverRevision: 1L,
                "{\"sku\":\"FEED-1\"}",
                Now);

            var feed2 = new SyncChangeFeedEntry(
                prodId,
                ProductChangeKind.Updated,
                serverRevision: 2L,
                "{\"sku\":\"FEED-1\",\"desc\":\"Updated\"}",
                Now.AddSeconds(5));

            context.SyncChangeFeedEntries.AddRange(feed1, feed2);
            await context.SaveChangesAsync();

            Assert.True(feed1.Sequence > 0);
            Assert.True(feed2.Sequence > feed1.Sequence);
        }
    }

    [Fact]
    public async Task Persistence_StoresAndRetrievesCausalMetadataAndUtcTimestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<RetailLabDbContext>()
            .UseSqlite(connection)
            .Options;

        var opId = Guid.NewGuid();
        var prodId = Guid.NewGuid();
        var predId = Guid.NewGuid();
        var localTimeWithOffset = new DateTimeOffset(2026, 10, 10, 18, 30, 0, TimeSpan.FromHours(3));

        await using (var context = new RetailLabDbContext(options))
        {
            await context.Database.MigrateAsync();

            var outbox = new SyncOutboxEntry(
                opId,
                prodId,
                SyncOperationKind.UpdateProductDetails,
                "{\"desc\":\"Changed\"}",
                baseServerRevision: null,
                predecessorOperationId: predId,
                localTimeWithOffset);

            outbox.MarkConflicted("Server rejected due to concurrent archive.");
            context.SyncOutboxEntries.Add(outbox);
            await context.SaveChangesAsync();
        }

        await using (var verificationContext = new RetailLabDbContext(options))
        {
            var loaded = await verificationContext.SyncOutboxEntries.FirstOrDefaultAsync(e => e.OperationId == opId);
            Assert.NotNull(loaded);
            Assert.Equal(prodId, loaded.ProductId);
            Assert.Equal(SyncOperationKind.UpdateProductDetails, loaded.Kind);
            Assert.Null(loaded.BaseServerRevision);
            Assert.Equal(predId, loaded.PredecessorOperationId);
            Assert.Equal(OutboxProcessingState.Conflicted, loaded.State);
            Assert.Equal("Server rejected due to concurrent archive.", loaded.RecordedResult);
            // Verify UTC normalization
            Assert.Equal(TimeSpan.Zero, loaded.CreatedAtUtc.Offset);
            Assert.Equal(localTimeWithOffset.ToUniversalTime(), loaded.CreatedAtUtc);
        }
    }
}
