namespace RetailLab.Sync;

public sealed class SyncClientOptions
{
    public const int MaximumPageSize = 500;

    public SyncClientOptions(Uri serverBaseUrl, int pageSize = 100, TimeSpan? timeout = null)
    {
        ArgumentNullException.ThrowIfNull(serverBaseUrl);
        if (!serverBaseUrl.IsAbsoluteUri ||
            (!serverBaseUrl.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) &&
             !serverBaseUrl.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
        {
            throw new SyncConfigurationException("The sync server URL must be an absolute HTTP or HTTPS URL.");
        }

        if (pageSize is <= 0 or > MaximumPageSize)
        {
            throw new SyncConfigurationException(
                $"The page size must be between 1 and {MaximumPageSize}.");
        }

        var resolvedTimeout = timeout ?? TimeSpan.FromSeconds(30);
        if (resolvedTimeout <= TimeSpan.Zero)
        {
            throw new SyncConfigurationException("The sync timeout must be greater than zero.");
        }

        ServerBaseUrl = serverBaseUrl.AbsoluteUri.EndsWith("/", StringComparison.Ordinal)
            ? serverBaseUrl
            : new Uri(serverBaseUrl.AbsoluteUri + "/", UriKind.Absolute);
        PageSize = pageSize;
        Timeout = resolvedTimeout;
    }

    public Uri ServerBaseUrl { get; }

    public int PageSize { get; }

    public TimeSpan Timeout { get; }
}
