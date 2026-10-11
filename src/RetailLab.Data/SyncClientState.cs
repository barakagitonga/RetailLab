namespace RetailLab.Data;

/// <summary>
/// Singleton client pairing state and cursor metadata stored on the client device.
/// Enforced as a singleton row (Id = 1).
/// </summary>
public sealed class SyncClientState
{
    public const int SingletonId = 1;
    public const int MaximumServerIdLength = 200;

    private SyncClientState()
    {
    }

    public SyncClientState(Guid deviceId)
    {
        if (deviceId == Guid.Empty)
        {
            throw new ArgumentException("Device ID cannot be empty.", nameof(deviceId));
        }

        Id = SingletonId;
        DeviceId = deviceId;
        LastPulledSequence = 0;
    }

    public int Id { get; private set; } = SingletonId;

    public Guid DeviceId { get; private set; }

    public string? PairedServerId { get; private set; }

    public long LastPulledSequence { get; private set; }

    public DateTimeOffset? PairedAtUtc { get; private set; }

    public DateTimeOffset? LastSyncAtUtc { get; private set; }

    public void Pair(string pairedServerId, DateTimeOffset pairedAtUtc, long initialSequence = 0)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pairedServerId);
        if (initialSequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialSequence),
                "Initial sequence cannot be negative.");
        }

        var normalized = pairedServerId.Trim();
        if (normalized.Length > MaximumServerIdLength)
        {
            throw new ArgumentException(
                $"Server ID cannot exceed {MaximumServerIdLength} characters.",
                nameof(pairedServerId));
        }

        PairedServerId = normalized;
        PairedAtUtc = pairedAtUtc.ToUniversalTime();
        LastPulledSequence = initialSequence;
    }

    public void UpdateLastPulledSequence(long sequence)
    {
        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sequence),
                "Sequence cannot be negative.");
        }

        LastPulledSequence = sequence;
    }

    public void RecordSuccessfulSync(DateTimeOffset syncAtUtc, long? lastPulledSequence = null)
    {
        LastSyncAtUtc = syncAtUtc.ToUniversalTime();
        if (lastPulledSequence.HasValue)
        {
            UpdateLastPulledSequence(lastPulledSequence.Value);
        }
    }
}
