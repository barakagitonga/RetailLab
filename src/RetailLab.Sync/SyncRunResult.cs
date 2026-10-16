namespace RetailLab.Sync;

public sealed record SyncRunResult(
    bool PairedThisRun,
    int PagesApplied,
    int EntriesApplied,
    long Cursor,
    long ObservedHead);

public abstract record SyncProgress;

public sealed record SyncPairedProgress(Guid ServerInstanceId, long HeadSequence) : SyncProgress;

public sealed record SyncPageAppliedProgress(
    long FirstSequence,
    long LastSequence,
    int EntryCount,
    long ObservedHead) : SyncProgress;
