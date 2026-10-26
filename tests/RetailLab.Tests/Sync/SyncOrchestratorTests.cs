using RetailLab.Core;
using RetailLab.Sync;

namespace RetailLab.Tests.Sync;

public sealed class SyncOrchestratorTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 16, 11, 0, 0, TimeSpan.Zero);
    private static readonly SyncClientOptions Options =
        new(new Uri("http://localhost:5197"), pageSize: 2);

    [Fact]
    public async Task RunAsync_FirstRunPairsAndPullsThenSecondRunOnlyPulls()
    {
        var productId = Guid.NewGuid();
        var serverId = Guid.NewGuid();
        var transport = new FakeTransport(
            new PairResponse(serverId, Now, 1),
            new PullResponse(
                [ProductServerSnapshotTests.CreateEntry(1, productId, 1)],
                1,
                false),
            new PullResponse([], 1, false));
        var store = new FakeStore();
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now.AddMinutes(5)));

        var first = await orchestrator.RunAsync();
        var second = await orchestrator.RunAsync();

        Assert.True(first.PairedThisRun);
        Assert.False(second.PairedThisRun);
        Assert.Equal(1, transport.PairCallCount);
        Assert.Equal(2, transport.PullCallCount);
        Assert.Equal(1, store.Cursor);
        Assert.Equal(Now.AddMinutes(5), store.LastSyncAtUtc);
    }

    [Fact]
    public async Task RunAsync_GappedPageStopsBeforeStoreApplication()
    {
        var store = FakeStore.CreatePaired(Guid.NewGuid(), Guid.NewGuid(), cursor: 0);
        var entry = ProductServerSnapshotTests.CreateEntry(2, Guid.NewGuid(), 1);
        var transport = new FakeTransport(
            pairResponse: null,
            new PullResponse([entry], 2, false));
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<SyncFeedGapException>(
            () => orchestrator.RunAsync());

        Assert.Equal(1, exception.ExpectedSequence);
        Assert.Equal(2, exception.ActualSequence);
        Assert.Equal(0, store.ApplyPageCallCount);
        Assert.Equal(0, store.Cursor);
    }

    [Fact]
    public async Task RunAsync_PendingLocalWorkUploadsBeforeNetworkPull()
    {
        var store = FakeStore.CreatePaired(Guid.NewGuid(), Guid.NewGuid(), cursor: 4);
        var operation = new CreateProductOperation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "OFFLINE-1",
            "Offline product",
            10m,
            2,
            Now);
        store.Pending.Add(new PendingSyncOperation(1, operation));
        var transport = new FakeTransport(
            pairResponse: null,
            new PullResponse([], 4, false));
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        var result = await orchestrator.RunAsync();

        Assert.Equal(1, transport.UploadCallCount);
        Assert.Equal(1, transport.PullCallCount);
        Assert.Equal(1, store.ApplyUploadResultsCallCount);
        Assert.Equal(1, result.OperationsUploaded);
    }

    [Fact]
    public async Task RunAsync_NonEmptyUnpairedDatabaseStopsBeforePairing()
    {
        var store = new FakeStore
        {
            Eligibility = new EligibilityReport(2, 0, 0)
        };
        var transport = new FakeTransport(pairResponse: null);
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        var exception = await Assert.ThrowsAsync<DatabaseNotEligibleForPairingException>(
            () => orchestrator.RunAsync());

        Assert.Equal(2, exception.ProductCount);
        Assert.Equal(0, transport.PairCallCount);
    }

    [Fact]
    public async Task RunAsync_EmptyPageBehindHeadIsRejectedWithoutMovingCursor()
    {
        var store = FakeStore.CreatePaired(Guid.NewGuid(), Guid.NewGuid(), cursor: 3);
        var transport = new FakeTransport(
            pairResponse: null,
            new PullResponse([], 4, false));
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        await Assert.ThrowsAsync<SyncProtocolException>(() => orchestrator.RunAsync());

        Assert.Equal(3, store.Cursor);
        Assert.Equal(0, store.MarkSyncedCallCount);
    }

    [Fact]
    public async Task RunAsync_AdjustmentAcceptedWithoutSnapshotStopsBeforeLocalWriteAndPull()
    {
        var serverId = Guid.NewGuid();
        var store = FakeStore.CreatePaired(Guid.NewGuid(), serverId, cursor: 0);
        var operation = AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(),
            Guid.NewGuid(),
            2,
            SynchronizedAdjustmentReasons.Restock,
            1,
            Now);
        store.Pending.Add(new PendingSyncOperation(1, operation));
        var transport = new FakeTransport(pairResponse: null)
        {
            UploadResponseOverride = new UploadResponse(
                SyncProtocolVersions.Upload,
                serverId,
                Now,
                [new UploadOperationResult(
                    operation.OperationId,
                    SyncOperationOutcome.Accepted,
                    null,
                    2,
                    null,
                    false)])
        };
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        await Assert.ThrowsAsync<SyncProtocolException>(() => orchestrator.RunAsync());

        Assert.Equal(0, store.ApplyUploadResultsCallCount);
        Assert.Equal(0, transport.PullCallCount);
        Assert.Single(store.Pending);
    }

    [Fact]
    public async Task RunAsync_ValidAcceptedAdjustmentAppliesOutcomeBeforePull()
    {
        var serverId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var store = FakeStore.CreatePaired(Guid.NewGuid(), serverId, cursor: 0);
        var operation = AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(),
            productId,
            2,
            SynchronizedAdjustmentReasons.Restock,
            1,
            Now);
        store.Pending.Add(new PendingSyncOperation(1, operation));
        var transport = new FakeTransport(
            pairResponse: null,
            new PullResponse([], 0, false))
        {
            UploadResponseOverride = new UploadResponse(
                SyncProtocolVersions.Upload,
                serverId,
                Now,
                [new UploadOperationResult(
                    operation.OperationId,
                    SyncOperationOutcome.Accepted,
                    null,
                    2,
                    new ProductSnapshot(
                        productId,
                        "SYNC-ADJ",
                        "Adjusted product",
                        10m,
                        7,
                        false,
                        null,
                        2),
                    false)])
        };
        var orchestrator = new SyncOrchestrator(
            transport,
            store,
            Options,
            new TestTimeProvider(Now));

        await orchestrator.RunAsync();

        Assert.Equal(1, store.ApplyUploadResultsCallCount);
        Assert.Equal(1, transport.PullCallCount);
        Assert.Empty(store.Pending);
    }

    [Fact]
    public async Task RunAsync_ArchivedAcceptedAdjustmentSnapshotIsAProtocolError()
    {
        var serverId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var store = FakeStore.CreatePaired(Guid.NewGuid(), serverId, cursor: 0);
        var operation = AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(), productId, 1, SynchronizedAdjustmentReasons.Restock, 1, Now);
        store.Pending.Add(new PendingSyncOperation(1, operation));
        var transport = new FakeTransport(pairResponse: null)
        {
            UploadResponseOverride = new UploadResponse(
                SyncProtocolVersions.Upload,
                serverId,
                Now,
                [new UploadOperationResult(
                    operation.OperationId,
                    SyncOperationOutcome.Accepted,
                    null,
                    2,
                    new ProductSnapshot(
                        productId, "ARCH-1", "Archived", 10m, 4, true, Now, 2),
                    false)])
        };
        var orchestrator = new SyncOrchestrator(
            transport, store, Options, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<SyncProtocolException>(() => orchestrator.RunAsync());

        Assert.Equal(0, store.ApplyUploadResultsCallCount);
        Assert.Equal(0, transport.PullCallCount);
    }

    [Fact]
    public async Task RunAsync_InsufficientStockWithoutSnapshotIsAProtocolError()
    {
        var serverId = Guid.NewGuid();
        var store = FakeStore.CreatePaired(Guid.NewGuid(), serverId, cursor: 0);
        var operation = AdjustStockOperation.BasedOnServer(
            Guid.NewGuid(),
            Guid.NewGuid(),
            -2,
            SynchronizedAdjustmentReasons.Damage,
            1,
            Now);
        store.Pending.Add(new PendingSyncOperation(1, operation));
        var transport = new FakeTransport(pairResponse: null)
        {
            UploadResponseOverride = new UploadResponse(
                SyncProtocolVersions.Upload,
                serverId,
                Now,
                [new UploadOperationResult(
                    operation.OperationId,
                    SyncOperationOutcome.Conflict,
                    "insufficient_stock",
                    null,
                    null,
                    false)])
        };
        var orchestrator = new SyncOrchestrator(
            transport, store, Options, new TestTimeProvider(Now));

        await Assert.ThrowsAsync<SyncProtocolException>(() => orchestrator.RunAsync());

        Assert.Equal(0, store.ApplyUploadResultsCallCount);
        Assert.Equal(0, transport.PullCallCount);
    }

    private sealed class FakeTransport : ISyncServerTransport
    {
        private readonly PairResponse? _pairResponse;
        private readonly Queue<PullResponse> _pages;

        public FakeTransport(PairResponse? pairResponse, params PullResponse[] pages)
        {
            _pairResponse = pairResponse;
            _pages = new Queue<PullResponse>(pages);
        }

        public int PairCallCount { get; private set; }

        public int PullCallCount { get; private set; }

        public int UploadCallCount { get; private set; }

        public UploadResponse? UploadResponseOverride { get; init; }

        public Task<PairResponse> PairAsync(
            PairRequest request,
            CancellationToken cancellationToken = default)
        {
            PairCallCount++;
            return Task.FromResult(
                _pairResponse ?? throw new InvalidOperationException("Pairing was not expected."));
        }

        public Task<PullResponse> PullAsync(
            PullRequest request,
            CancellationToken cancellationToken = default)
        {
            PullCallCount++;
            return Task.FromResult(_pages.Dequeue());
        }

        public Task<UploadResponse> UploadAsync(
            UploadRequest request,
            CancellationToken cancellationToken = default)
        {
            UploadCallCount++;
            if (UploadResponseOverride is not null)
            {
                return Task.FromResult(UploadResponseOverride);
            }

            var results = request.Operations
                .Select(operation => new UploadOperationResult(
                    operation.OperationId,
                    SyncOperationOutcome.Accepted,
                    code: null,
                    resultingServerRevision: 1,
                    snapshot: null,
                    replay: false))
                .ToList();
            return Task.FromResult(new UploadResponse(
                SyncProtocolVersions.Upload,
                request.ServerInstanceId,
                Now,
                results));
        }
    }

    private sealed class FakeStore : ILocalSyncStore
    {
        public SyncCheckpoint? Checkpoint { get; private set; }

        public EligibilityReport Eligibility { get; set; } = new(0, 0, 0);

        public bool HasPendingWork { get; set; }

        public List<PendingSyncOperation> Pending { get; } = [];

        public int ApplyPageCallCount { get; private set; }

        public int MarkSyncedCallCount { get; private set; }

        public int ApplyUploadResultsCallCount { get; private set; }

        public long Cursor => Checkpoint?.LastPulledSequence ?? 0;

        public DateTimeOffset? LastSyncAtUtc => Checkpoint?.LastSyncAtUtc;

        public static FakeStore CreatePaired(Guid deviceId, Guid serverId, long cursor)
        {
            return new FakeStore
            {
                Checkpoint = new SyncCheckpoint(deviceId, serverId, cursor, Now, null)
            };
        }

        public Task<SyncCheckpoint?> GetCheckpointAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(Checkpoint);

        public Task<EligibilityReport> CheckEligibilityAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(Eligibility);

        public Task<bool> HasPendingOutboxWorkAsync(
            CancellationToken cancellationToken = default) => Task.FromResult(HasPendingWork);

        public Task<IReadOnlyList<PendingSyncOperation>> GetPendingOperationsAsync(
            int limit,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<PendingSyncOperation>>(Pending.Take(limit).ToList());

        public Task ApplyUploadResultsAsync(
            IReadOnlyList<UploadOperationResult> results,
            CancellationToken cancellationToken = default)
        {
            ApplyUploadResultsCallCount++;
            var ids = results.Select(result => result.OperationId).ToHashSet();
            Pending.RemoveAll(item => ids.Contains(item.Operation.OperationId));
            return Task.CompletedTask;
        }

        public Task<ProductSyncState?> GetProductSyncStateAsync(
            Guid productId,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Offline capture was not expected.");

        public Task CaptureOperationAsync(
            ISyncProductOperation operation,
            CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Offline capture was not expected.");

        public Task MarkPairedAsync(
            Guid deviceId,
            Guid serverInstanceId,
            DateTimeOffset pairedAtUtc,
            CancellationToken cancellationToken = default)
        {
            Checkpoint = new SyncCheckpoint(deviceId, serverInstanceId, 0, pairedAtUtc, null);
            return Task.CompletedTask;
        }

        public Task ApplyPageAsync(
            IReadOnlyList<ServerChangeEntry> entries,
            long expectedCursor,
            CancellationToken cancellationToken = default)
        {
            ApplyPageCallCount++;
            Checkpoint = Checkpoint! with { LastPulledSequence = entries[^1].Sequence };
            return Task.CompletedTask;
        }

        public Task MarkSyncedAsync(
            DateTimeOffset syncedAtUtc,
            CancellationToken cancellationToken = default)
        {
            MarkSyncedCallCount++;
            Checkpoint = Checkpoint! with { LastSyncAtUtc = syncedAtUtc };
            return Task.CompletedTask;
        }
    }
}
