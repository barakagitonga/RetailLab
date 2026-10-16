namespace RetailLab.Core;

/// <summary>
/// Transport-independent view of a client database's durable pairing state.
/// </summary>
public sealed record SyncCheckpoint(
    Guid DeviceId,
    Guid? ServerInstanceId,
    long LastPulledSequence,
    DateTimeOffset? PairedAtUtc,
    DateTimeOffset? LastSyncAtUtc);
