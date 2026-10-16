namespace RetailLab.Core;

/// <summary>
/// Identifies a local client database asking to pair with an authoritative server.
/// Device authentication is deliberately deferred; the client identifier is
/// informational in Tutorial 7C-i.
/// </summary>
public sealed record PairRequest(Guid ClientInstanceId, bool DeclaresEmpty);

/// <summary>
/// Stable server identity and current feed position returned during pairing.
/// </summary>
public sealed record PairResponse(
    Guid ServerInstanceId,
    DateTimeOffset ServerTimeUtc,
    long HeadSequence);

/// <summary>
/// One ordered page from the authoritative product change feed.
/// </summary>
public sealed record PullResponse(
    IReadOnlyList<ServerChangeEntry> Entries,
    long HeadSequence,
    bool HasMore);
