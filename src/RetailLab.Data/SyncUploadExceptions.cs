namespace RetailLab.Data;

public sealed class SyncOperationIdConflictException(string code) : Exception(code)
{
    public string Code { get; } = code;
}

public sealed class SyncUploadTransientException(Exception innerException)
    : Exception("The upload operation could not reach a durable decision.", innerException);
