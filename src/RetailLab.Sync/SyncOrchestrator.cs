using RetailLab.Core;

namespace RetailLab.Sync;

public sealed class SyncOrchestrator(
    ISyncServerTransport transport,
    ILocalSyncStore store,
    SyncClientOptions options,
    TimeProvider timeProvider)
{
    public async Task<SyncRunResult> RunAsync(
        IProgress<SyncProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var checkpoint = await store.GetCheckpointAsync(cancellationToken);
        var pairedThisRun = false;

        if (checkpoint?.ServerInstanceId is null)
        {
            var eligibility = await store.CheckEligibilityAsync(cancellationToken);
            if (eligibility.HasProducts || eligibility.HasOutboxEntries)
            {
                throw new DatabaseNotEligibleForPairingException(
                    eligibility.ProductCount,
                    eligibility.OutboxEntryCount);
            }

            var deviceId = checkpoint?.DeviceId ?? Guid.NewGuid();
            var pairing = await transport.PairAsync(
                new PairRequest(deviceId, DeclaresEmpty: true),
                cancellationToken);

            try
            {
                await store.MarkPairedAsync(
                    deviceId,
                    pairing.ServerInstanceId,
                    pairing.ServerTimeUtc,
                    cancellationToken);
            }
            catch (SyncEligibilityException exception)
            {
                throw new DatabaseNotEligibleForPairingException(
                    exception.ProductCount,
                    exception.OutboxEntryCount);
            }

            checkpoint = new SyncCheckpoint(
                deviceId,
                pairing.ServerInstanceId,
                0,
                pairing.ServerTimeUtc,
                null);
            pairedThisRun = true;
            progress?.Report(new SyncPairedProgress(pairing.ServerInstanceId, pairing.HeadSequence));
        }

        if (await store.HasPendingOutboxWorkAsync(cancellationToken))
        {
            throw new PendingLocalSyncWorkException();
        }

        var serverInstanceId = checkpoint.ServerInstanceId
            ?? throw new SyncLocalStateException("The client checkpoint has no server identity.");
        var cursor = checkpoint.LastPulledSequence;
        var observedHead = cursor;
        var pagesApplied = 0;
        var entriesApplied = 0;

        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var page = await transport.PullAsync(
                new PullRequest(checkpoint.DeviceId, serverInstanceId, cursor, options.PageSize),
                cancellationToken);
            observedHead = page.HeadSequence;

            if (page.Entries.Count == 0)
            {
                if (page.HasMore)
                {
                    throw new SyncProtocolException(
                        "The server reported more pages but returned no entries.");
                }

                if (cursor != page.HeadSequence)
                {
                    throw new SyncProtocolException(
                        "The server returned an empty page while its feed head is ahead of the local cursor.");
                }

                break;
            }

            for (var index = 0; index < page.Entries.Count; index++)
            {
                var expectedSequence = checked(cursor + index + 1L);
                var actualSequence = page.Entries[index].Sequence;
                if (actualSequence != expectedSequence)
                {
                    throw new SyncFeedGapException(expectedSequence, actualSequence);
                }
            }

            var previousCursor = cursor;
            var newCursor = page.Entries[^1].Sequence;
            try
            {
                await store.ApplyPageAsync(page.Entries, cursor, cancellationToken);
            }
            catch (SyncPageValidationException exception)
            {
                throw new SyncFeedGapException(
                    exception.ExpectedSequence,
                    exception.ActualSequence);
            }
            catch (BusinessRuleException exception)
            {
                throw new SyncProtocolException(
                    "The server supplied a product snapshot that violates RetailLab business rules.",
                    exception);
            }
            catch (ArgumentException exception)
            {
                throw new SyncProtocolException(
                    "The server supplied an invalid product snapshot.",
                    exception);
            }

            cursor = newCursor;
            pagesApplied++;
            entriesApplied += page.Entries.Count;
            progress?.Report(new SyncPageAppliedProgress(
                previousCursor + 1,
                cursor,
                page.Entries.Count,
                page.HeadSequence));

            if (page.HasMore)
            {
                continue;
            }

            if (cursor < page.HeadSequence)
            {
                throw new SyncProtocolException(
                    "The server reported no more pages while its feed head is still ahead.");
            }

            break;
        }

        await store.MarkSyncedAsync(timeProvider.GetUtcNow(), cancellationToken);
        return new SyncRunResult(
            pairedThisRun,
            pagesApplied,
            entriesApplied,
            cursor,
            Math.Max(cursor, observedHead));
    }
}
