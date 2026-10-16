namespace RetailLab.Core;

public sealed class PendingLocalSyncWorkException : Exception
{
    public PendingLocalSyncWorkException()
        : base("Pending local operations must be uploaded before authoritative changes can be pulled.")
    {
    }
}

public sealed class SyncCheckpointConflictException : Exception
{
    public SyncCheckpointConflictException(string message)
        : base(message)
    {
    }
}

public sealed class SyncPageValidationException : Exception
{
    public SyncPageValidationException(long expectedSequence, long actualSequence)
        : base($"Expected feed sequence {expectedSequence}, but received {actualSequence}.")
    {
        ExpectedSequence = expectedSequence;
        ActualSequence = actualSequence;
    }

    public long ExpectedSequence { get; }

    public long ActualSequence { get; }
}

public sealed class SyncLocalStateException : Exception
{
    public SyncLocalStateException(string message)
        : base(message)
    {
    }
}

public sealed class SyncEligibilityException : Exception
{
    public SyncEligibilityException(long productCount, long outboxEntryCount)
        : base("The local database stopped being eligible for pairing before pairing could be saved.")
    {
        ProductCount = productCount;
        OutboxEntryCount = outboxEntryCount;
    }

    public long ProductCount { get; }

    public long OutboxEntryCount { get; }
}
