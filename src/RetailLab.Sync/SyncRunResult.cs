namespace RetailLab.Sync;

public sealed record SyncRunResult(
    bool PairedThisRun,
    int PagesApplied,
    int EntriesApplied,
    long Cursor,
    long ObservedHead,
    int UploadBatches = 0,
    int OperationsUploaded = 0);

public abstract record SyncProgress;

public sealed record SyncPairedProgress(Guid ServerInstanceId, long HeadSequence) : SyncProgress;

public sealed record SyncUploadBatchAppliedProgress(
    int OperationCount,
    int AcceptedCount,
    int ConflictCount,
    int RejectedCount) : SyncProgress;

public sealed record SyncPageAppliedProgress(
    long FirstSequence,
    long LastSequence,
    int EntryCount,
    long ObservedHead) : SyncProgress;
