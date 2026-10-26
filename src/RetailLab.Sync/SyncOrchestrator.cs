using RetailLab.Core;

namespace RetailLab.Sync;

public sealed class SyncOrchestrator(
    ISyncServerTransport transport,
    ILocalSyncStore store,
    SyncClientOptions options,
    TimeProvider timeProvider)
{
    public const int UploadBatchSize = 25;

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

        var serverInstanceId = checkpoint.ServerInstanceId
            ?? throw new SyncLocalStateException("The client checkpoint has no server identity.");
        var uploadBatches = 0;
        var operationsUploaded = 0;
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pending = await store.GetPendingOperationsAsync(UploadBatchSize, cancellationToken);
            if (pending.Count == 0)
            {
                break;
            }

            var operations = pending.Select(item => item.Operation).ToList();
            var upload = await transport.UploadAsync(
                new UploadRequest(checkpoint.DeviceId, serverInstanceId, operations),
                cancellationToken);
            ValidateUploadResponse(upload, serverInstanceId, operations);
            await store.ApplyUploadResultsAsync(upload.Results, cancellationToken);

            uploadBatches++;
            operationsUploaded += operations.Count;
            progress?.Report(new SyncUploadBatchAppliedProgress(
                operations.Count,
                upload.Results.Count(result => result.Outcome == SyncOperationOutcome.Accepted),
                upload.Results.Count(result => result.Outcome == SyncOperationOutcome.Conflict),
                upload.Results.Count(result => result.Outcome == SyncOperationOutcome.Rejected)));
        }

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
            Math.Max(cursor, observedHead),
            uploadBatches,
            operationsUploaded);
    }

    private static void ValidateUploadResponse(
        UploadResponse response,
        Guid expectedServerInstanceId,
        IReadOnlyList<ISyncProductOperation> operations)
    {
        if (response.ProtocolVersion != SyncProtocolVersions.Upload)
        {
            throw new SyncProtocolException("The server returned an unsupported upload protocol version.");
        }

        if (response.ServerInstanceId != expectedServerInstanceId)
        {
            throw new ServerIdentityChangedException();
        }

        if (response.Results.Count != operations.Count)
        {
            throw new SyncProtocolException("The upload response does not contain one result per operation.");
        }

        for (var index = 0; index < operations.Count; index++)
        {
            var operation = operations[index];
            var result = response.Results[index];
            if (result.OperationId != operation.OperationId)
            {
                throw new SyncProtocolException(
                    "The upload response operation order does not match the submitted batch.");
            }

            if (operation is AdjustStockOperation adjustment)
            {
                ValidateAdjustmentResult(adjustment, result);
            }
        }
    }

    private static void ValidateAdjustmentResult(
        AdjustStockOperation operation,
        UploadOperationResult result)
    {
        if (result.Outcome == SyncOperationOutcome.Accepted)
        {
            if (result.Code is not null ||
                result.ResultingServerRevision is not > 0 ||
                result.Snapshot is null ||
                result.Snapshot.Id != operation.ProductId ||
                result.Snapshot.ServerRevision != result.ResultingServerRevision ||
                !IsValidActiveSnapshot(result.Snapshot))
            {
                throw new SyncProtocolException(
                    "The server returned an invalid Accepted result for a stock adjustment.");
            }

            return;
        }

        if (result.Outcome == SyncOperationOutcome.Conflict &&
            string.Equals(result.Code, "insufficient_stock", StringComparison.Ordinal))
        {
            if (result.ResultingServerRevision.HasValue ||
                result.Snapshot is null ||
                result.Snapshot.Id != operation.ProductId ||
                !IsValidActiveSnapshot(result.Snapshot))
            {
                throw new SyncProtocolException(
                    "The server returned invalid insufficient-stock evidence for a stock adjustment.");
            }
        }
    }

    private static bool IsValidActiveSnapshot(ProductSnapshot snapshot) =>
        snapshot.Id != Guid.Empty &&
        !string.IsNullOrWhiteSpace(snapshot.Sku) &&
        snapshot.Sku.Trim().Length <= Product.MaximumSkuLength &&
        !string.IsNullOrWhiteSpace(snapshot.Description) &&
        snapshot.Description.Trim().Length <= Product.MaximumDescriptionLength &&
        snapshot.Price >= 0 &&
        snapshot.StockQuantity >= 0 &&
        snapshot.ServerRevision > 0 &&
        !snapshot.IsArchived &&
        snapshot.ArchivedAtUtc is null;
}
