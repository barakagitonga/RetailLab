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
