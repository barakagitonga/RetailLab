namespace RetailLab.Core;

/// <summary>
/// Identifies the kind of authoritative change published to the server change feed.
/// </summary>
public enum ProductChangeKind
{
    Created = 1,
    Updated = 2,
    Archived = 3,
    Unarchived = 4
}
