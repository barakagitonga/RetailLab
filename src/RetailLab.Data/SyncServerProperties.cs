namespace RetailLab.Data;

/// <summary>
/// Singleton identity for one authoritative server database. Unlike a URL,
/// this value remains stable when the server moves or changes ports.
/// </summary>
public sealed class SyncServerProperties
{
    public const int SingletonId = 1;

    private SyncServerProperties()
    {
    }

    public SyncServerProperties(Guid serverInstanceId, DateTimeOffset createdAtUtc)
    {
        if (serverInstanceId == Guid.Empty)
        {
            throw new ArgumentException("Server instance ID cannot be empty.", nameof(serverInstanceId));
        }

        Id = SingletonId;
        ServerInstanceId = serverInstanceId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public int Id { get; private set; } = SingletonId;

    public Guid ServerInstanceId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }
}
