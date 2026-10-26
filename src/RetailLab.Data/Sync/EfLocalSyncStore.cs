using Microsoft.EntityFrameworkCore;
using RetailLab.Core;

namespace RetailLab.Data;

/// <summary>
/// Applies authoritative pages to a client database. Every public operation
/// uses a fresh base context; server publication is therefore impossible here.
/// </summary>
public sealed class EfLocalSyncStore(Func<RetailLabDbContext> createDbContext) : ILocalSyncStore
{
    public async Task<SyncCheckpoint?> GetCheckpointAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var state = await dbContext.SyncClientStates
            .AsNoTracking()
            .SingleOrDefaultAsync(
                item => item.Id == SyncClientState.SingletonId,
                cancellationToken);

        return state is null ? null : ToCheckpoint(state);
    }

    public async Task<EligibilityReport> CheckEligibilityAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        return await ReadEligibilityAsync(dbContext, cancellationToken);
    }

    public async Task<bool> HasPendingOutboxWorkAsync(
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        return await dbContext.SyncOutboxEntries
            .AnyAsync(entry => entry.State == OutboxProcessingState.Pending, cancellationToken);
    }

    public async Task<IReadOnlyList<PendingSyncOperation>> GetPendingOperationsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        if (limit <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        await using var dbContext = createDbContext();
        var entries = await dbContext.SyncOutboxEntries
            .AsNoTracking()
            .Where(entry => entry.State == OutboxProcessingState.Pending)
            .OrderBy(entry => entry.LocalSequence)
            .Take(limit)
            .ToListAsync(cancellationToken);
        return entries
            .Select(entry => new PendingSyncOperation(
                entry.LocalSequence,
                SyncOperationSerializer.Deserialize(entry)))
            .ToList();
    }

    public async Task ApplyUploadResultsAsync(
        IReadOnlyList<UploadOperationResult> results,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);
        if (results.Count == 0)
        {
            throw new ArgumentException("At least one upload result is required.", nameof(results));
        }

        var duplicate = results
            .GroupBy(result => result.OperationId)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new SyncLocalStateException(
                $"The upload response repeats operation {duplicate.Key:D}.");
        }

        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var resultById = results.ToDictionary(result => result.OperationId);
        var entries = await dbContext.SyncOutboxEntries
            .Where(entry => resultById.Keys.Contains(entry.OperationId))
            .OrderBy(entry => entry.LocalSequence)
            .ToListAsync(cancellationToken);

        if (entries.Count != results.Count || entries.Any(entry => entry.State != OutboxProcessingState.Pending))
        {
            throw new SyncLocalStateException(
                "The upload response does not match the currently pending outbox entries.");
        }

        foreach (var entry in entries)
        {
            var result = resultById[entry.OperationId];
            var recordedResult = System.Text.Json.JsonSerializer.Serialize(result);
            switch (result.Outcome)
            {
                case SyncOperationOutcome.Accepted:
                    if (entry.Kind == SyncOperationKind.AdjustStock)
                    {
                        await ApplyAcceptedAdjustmentAsync(
                            dbContext,
                            entry,
                            result,
                            cancellationToken);
                    }
                    entry.MarkProcessed(recordedResult);
                    break;
                case SyncOperationOutcome.Conflict:
                    entry.MarkConflicted(recordedResult);
                    break;
                case SyncOperationOutcome.Rejected:
                    entry.MarkRejected(recordedResult);
                    break;
                default:
                    throw new SyncLocalStateException("The upload response contains an unknown outcome.");
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ProductSyncState?> GetProductSyncStateAsync(
        Guid productId,
        CancellationToken cancellationToken = default)
    {
        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        await using var dbContext = createDbContext();
        var revision = await dbContext.Products
            .AsNoTracking()
            .Where(product => product.Id == productId)
            .Select(product => (long?)product.ServerRevision)
            .SingleOrDefaultAsync(cancellationToken);
        if (!revision.HasValue)
        {
            return null;
        }

        var predecessor = await dbContext.SyncOutboxEntries
            .AsNoTracking()
            .Where(entry =>
                entry.ProductId == productId &&
                entry.State == OutboxProcessingState.Pending)
            .OrderByDescending(entry => entry.LocalSequence)
            .Select(entry => (Guid?)entry.OperationId)
            .FirstOrDefaultAsync(cancellationToken);
        return new ProductSyncState(productId, revision.Value, predecessor);
    }

    public async Task CaptureOperationAsync(
        ISyncProductOperation operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var state = await dbContext.SyncClientStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == SyncClientState.SingletonId, cancellationToken);
        if (state is null || string.IsNullOrWhiteSpace(state.PairedServerId))
        {
            throw new SyncLocalStateException(
                "This database must complete its first synchronization before offline edits are captured.");
        }

        Product product;
        if (operation is CreateProductOperation create)
        {
            if (await dbContext.Products.AnyAsync(item => item.Id == create.ProductId, cancellationToken))
            {
                throw new SyncLocalStateException("The locally generated product ID is already in use.");
            }
            product = new Product(
                create.ProductId,
                create.Sku,
                create.Description,
                create.Price,
                create.InitialStockQuantity);
            dbContext.Products.Add(product);
        }
        else
        {
            product = await dbContext.Products.SingleOrDefaultAsync(
                item => item.Id == operation.ProductId,
                cancellationToken)
                ?? throw new BusinessRuleException(
                    $"Product {operation.ProductId:D} was not found locally.");
            await ValidateCausalStateAsync(dbContext, product, (ICausalOperation)operation, cancellationToken);
            switch (operation)
            {
                case UpdateProductDetailsOperation update:
                    product.UpdateDetails(update.Description, update.Price);
                    break;
                case ArchiveProductOperation:
                    product.Archive(operation.CreatedAtUtc);
                    break;
                case UnarchiveProductOperation:
                    product.Unarchive();
                    break;
                case AdjustStockOperation adjustment:
                    if (product.IsArchived)
                    {
                        throw new BusinessRuleException(
                            $"Archived product {product.Sku} cannot be adjusted.");
                    }
                    product.AdjustStock(adjustment.Delta);
                    break;
                default:
                    throw new SyncLocalStateException("The operation kind cannot be captured locally.");
            }
        }

        var causal = operation as ICausalOperation;
        dbContext.SyncOutboxEntries.Add(new SyncOutboxEntry(
            operation.OperationId,
            operation.ProductId,
            operation.Kind,
            SyncOperationSerializer.SerializePayload(operation),
            causal?.BaseServerRevision,
            causal?.PredecessorOperationId,
            operation.CreatedAtUtc));
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task ApplyAcceptedAdjustmentAsync(
        RetailLabDbContext dbContext,
        SyncOutboxEntry entry,
        UploadOperationResult result,
        CancellationToken cancellationToken)
    {
        var operation = SyncOperationSerializer.Deserialize(entry) as AdjustStockOperation
            ?? throw new SyncLocalStateException(
                $"Outbox operation {entry.OperationId:D} is not a stock adjustment.");
        var snapshot = result.Snapshot
            ?? throw new SyncLocalStateException(
                $"Accepted stock adjustment {entry.OperationId:D} has no validated snapshot.");
        var state = await dbContext.SyncClientStates
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == SyncClientState.SingletonId, cancellationToken)
            ?? throw new SyncLocalStateException("The paired client checkpoint is missing.");
        var actorIdentifier = $"sync-device:{state.DeviceId:D}";
        var existing = await dbContext.InventoryAdjustments
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == operation.OperationId, cancellationToken);

        if (existing is not null)
        {
            var exactMatch =
                existing.ProductId == operation.ProductId &&
                existing.QuantityChange == operation.Delta &&
                existing.ResultingQuantity == snapshot.StockQuantity &&
                existing.Reason == operation.Reason &&
                existing.ActorIdentifier == actorIdentifier &&
                existing.CreatedAtUtc == operation.CreatedAtUtc;
            if (!exactMatch)
            {
                throw new SyncLocalStateException(
                    $"Inventory adjustment {operation.OperationId:D} conflicts with the pending operation.");
            }

            return;
        }

        var product = await dbContext.Products.SingleOrDefaultAsync(
            item => item.Id == operation.ProductId,
            cancellationToken)
            ?? throw new SyncLocalStateException(
                $"Product {operation.ProductId:D} is missing while its accepted adjustment is applied.");
        dbContext.InventoryAdjustments.Add(new InventoryAdjustment(
            operation.OperationId,
            product,
            operation.Delta,
            snapshot.StockQuantity,
            operation.Reason,
            actorIdentifier,
            operation.CreatedAtUtc));
    }

    private static async Task ValidateCausalStateAsync(
        RetailLabDbContext dbContext,
        Product product,
        ICausalOperation operation,
        CancellationToken cancellationToken)
    {
        var latestPending = await dbContext.SyncOutboxEntries
            .AsNoTracking()
            .Where(entry =>
                entry.ProductId == product.Id &&
                entry.State == OutboxProcessingState.Pending)
            .OrderByDescending(entry => entry.LocalSequence)
            .Select(entry => (Guid?)entry.OperationId)
            .FirstOrDefaultAsync(cancellationToken);
        var matches = latestPending.HasValue
            ? operation.PredecessorOperationId == latestPending && !operation.BaseServerRevision.HasValue
            : operation.BaseServerRevision == product.ServerRevision && !operation.PredecessorOperationId.HasValue;
        if (!matches)
        {
            throw new SyncCheckpointConflictException(
                "The product's pending-operation chain changed before the offline edit could be saved.");
        }
    }

    public async Task MarkPairedAsync(
        Guid deviceId,
        Guid serverInstanceId,
        DateTimeOffset pairedAtUtc,
        CancellationToken cancellationToken = default)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));
        }

        if (serverInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Server instance ID cannot be empty.", nameof(serverInstanceId));
        }

        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        var state = await dbContext.SyncClientStates.SingleOrDefaultAsync(
            item => item.Id == SyncClientState.SingletonId,
            cancellationToken);

        if (state is not null)
        {
            var checkpoint = ToCheckpoint(state);
            if (checkpoint.ServerInstanceId.HasValue)
            {
                throw new SyncCheckpointConflictException(
                    "The local database is already paired and cannot be paired again.");
            }

            if (checkpoint.DeviceId != deviceId)
            {
                throw new SyncCheckpointConflictException(
                    "The stored device identity changed before pairing could be saved.");
            }
        }

        var eligibility = await ReadEligibilityAsync(dbContext, cancellationToken);
        if (eligibility.HasProducts || eligibility.HasOutboxEntries)
        {
            throw new SyncEligibilityException(
                eligibility.ProductCount,
                eligibility.OutboxEntryCount);
        }

        if (state is null)
        {
            state = new SyncClientState(deviceId);
            dbContext.SyncClientStates.Add(state);
        }

        state.Pair(serverInstanceId.ToString("D"), pairedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task ApplyPageAsync(
        IReadOnlyList<ServerChangeEntry> entries,
        long expectedCursor,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
        {
            throw new ArgumentException("A page must contain at least one entry.", nameof(entries));
        }

        if (expectedCursor < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedCursor));
        }

        cancellationToken.ThrowIfCancellationRequested();

        await using var dbContext = createDbContext();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);

        // Once the transaction begins, finish or roll it back as one atomic unit.
        // Cancellation is observed again between pages by the orchestrator.
        var state = await dbContext.SyncClientStates.SingleOrDefaultAsync(
            item => item.Id == SyncClientState.SingletonId,
            CancellationToken.None)
            ?? throw new SyncLocalStateException("The paired client checkpoint is missing.");

        var checkpoint = ToCheckpoint(state);
        if (!checkpoint.ServerInstanceId.HasValue)
        {
            throw new SyncLocalStateException("The client checkpoint has not been paired.");
        }

        if (checkpoint.LastPulledSequence != expectedCursor)
        {
            throw new SyncCheckpointConflictException(
                $"The stored cursor is {checkpoint.LastPulledSequence}, not the expected {expectedCursor}.");
        }

        var hasPendingWork = await dbContext.SyncOutboxEntries.AnyAsync(
            entry => entry.State == OutboxProcessingState.Pending,
            CancellationToken.None);
        if (hasPendingWork)
        {
            throw new PendingLocalSyncWorkException();
        }

        for (var index = 0; index < entries.Count; index++)
        {
            var entry = entries[index];
            var expectedSequence = checked(expectedCursor + index + 1L);
            if (entry.Sequence != expectedSequence)
            {
                throw new SyncPageValidationException(expectedSequence, entry.Sequence);
            }

            var product = await dbContext.Products.FindAsync(
                [entry.ProductId],
                CancellationToken.None);
            if (product is null)
            {
                dbContext.Products.Add(Product.CreateFromServerSnapshot(entry));
            }
            else
            {
                product.ApplyServerSnapshot(entry);
            }
        }

        state.UpdateLastPulledSequence(entries[^1].Sequence);
        await dbContext.SaveChangesAsync(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
    }

    public async Task MarkSyncedAsync(
        DateTimeOffset syncedAtUtc,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = createDbContext();
        var state = await dbContext.SyncClientStates.SingleOrDefaultAsync(
            item => item.Id == SyncClientState.SingletonId,
            cancellationToken)
            ?? throw new SyncLocalStateException("The paired client checkpoint is missing.");

        var checkpoint = ToCheckpoint(state);
        if (!checkpoint.ServerInstanceId.HasValue)
        {
            throw new SyncLocalStateException("The client checkpoint has not been paired.");
        }

        state.RecordSuccessfulSync(syncedAtUtc);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static async Task<EligibilityReport> ReadEligibilityAsync(
        RetailLabDbContext dbContext,
        CancellationToken cancellationToken)
    {
        var productCount = await dbContext.Products.LongCountAsync(cancellationToken);
        var outboxEntryCount = await dbContext.SyncOutboxEntries.LongCountAsync(cancellationToken);
        var pendingOutboxCount = await dbContext.SyncOutboxEntries.LongCountAsync(
            entry => entry.State == OutboxProcessingState.Pending,
            cancellationToken);

        return new EligibilityReport(productCount, outboxEntryCount, pendingOutboxCount);
    }

    private static SyncCheckpoint ToCheckpoint(SyncClientState state)
    {
        if (state.DeviceId == Guid.Empty)
        {
            throw new SyncLocalStateException("The stored device identity is empty.");
        }

        if (state.PairedServerId is null)
        {
            if (state.PairedAtUtc.HasValue || state.LastPulledSequence != 0)
            {
                throw new SyncLocalStateException(
                    "An unpaired checkpoint must have no pairing timestamp and cursor zero.");
            }

            return new SyncCheckpoint(
                state.DeviceId,
                null,
                state.LastPulledSequence,
                state.PairedAtUtc,
                state.LastSyncAtUtc);
        }

        if (!Guid.TryParse(state.PairedServerId, out var serverInstanceId) ||
            serverInstanceId == Guid.Empty)
        {
            throw new SyncLocalStateException("The stored server identity is not a valid GUID.");
        }

        if (!state.PairedAtUtc.HasValue)
        {
            throw new SyncLocalStateException("A paired checkpoint requires a pairing timestamp.");
        }

        return new SyncCheckpoint(
            state.DeviceId,
            serverInstanceId,
            state.LastPulledSequence,
            state.PairedAtUtc,
            state.LastSyncAtUtc);
    }
}
