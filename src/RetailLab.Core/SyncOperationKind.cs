namespace RetailLab.Core;

/// <summary>
/// Identifies the business kind of a client-side synchronization operation.
/// </summary>
public enum SyncOperationKind
{
    CreateProduct = 1,
    UpdateProductDetails = 2,
    ArchiveProduct = 3,
    UnarchiveProduct = 4,
    AdjustStock = 5
}
