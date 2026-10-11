namespace RetailLab.Core;

/// <summary>
/// Synchronization contract representing a client-side product creation operation.
/// Product creation operates with no base revision and no predecessor.
/// </summary>
public sealed class CreateProductOperation : ISyncProductOperation
{
    public CreateProductOperation(
        Guid operationId,
        Guid productId,
        string sku,
        string description,
        decimal price,
        int initialStockQuantity,
        DateTimeOffset createdAtUtc)
    {
        if (operationId == Guid.Empty)
        {
            throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
        }

        if (productId == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(productId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sku, nameof(sku));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        var trimmedSku = sku.Trim();
        if (trimmedSku.Length > Product.MaximumSkuLength)
        {
            throw new ArgumentException(
                $"SKU cannot exceed {Product.MaximumSkuLength} characters.",
                nameof(sku));
        }

        var trimmedDescription = description.Trim();
        if (trimmedDescription.Length > Product.MaximumDescriptionLength)
        {
            throw new ArgumentException(
                $"Description cannot exceed {Product.MaximumDescriptionLength} characters.",
                nameof(description));
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        if (initialStockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(initialStockQuantity),
                "Initial stock quantity cannot be negative.");
        }

        OperationId = operationId;
        ProductId = productId;
        Sku = trimmedSku;
        Description = trimmedDescription;
        Price = price;
        InitialStockQuantity = initialStockQuantity;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid OperationId { get; }

    public Guid ProductId { get; }

    public SyncOperationKind Kind => SyncOperationKind.CreateProduct;

    public string Sku { get; }

    public string Description { get; }

    public decimal Price { get; }

    public int InitialStockQuantity { get; }

    public DateTimeOffset CreatedAtUtc { get; }
}
