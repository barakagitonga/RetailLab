namespace RetailLab.Core;

/// <summary>
/// Terminal outcome of an uploaded synchronization operation on the server.
/// </summary>
public enum SyncOperationOutcome
{
    Accepted = 1,
    Conflict = 2,
    Rejected = 3
}
