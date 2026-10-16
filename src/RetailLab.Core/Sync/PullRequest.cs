namespace RetailLab.Core;

/// <summary>
/// Requests one ordered page after a client's durable local cursor.
/// </summary>
public sealed record PullRequest(
    Guid ClientInstanceId,
    Guid ServerInstanceId,
    long After,
    int Limit);
