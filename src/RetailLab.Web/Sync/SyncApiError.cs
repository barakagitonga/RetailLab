namespace RetailLab.Web.Sync;

internal sealed record SyncApiError(
    string Code,
    string Message,
    long? HeadSequence = null);
