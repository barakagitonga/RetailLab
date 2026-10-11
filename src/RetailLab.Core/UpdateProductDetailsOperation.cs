namespace RetailLab.Core;

/// <summary>
/// Synchronization contract representing a client-side product details update.
/// Must specify either BaseServerRevision or PredecessorOperationId, but not both.
/// </summary>
public sealed class UpdateProductDetailsOperation : ICausalOperation
{
    public UpdateProductDetailsOperation(
        Guid operationId,
        Guid productId,
        string description,
        decimal price,
        long? baseServerRevision,
        Guid? predecessorOperationId,
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

        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

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

        CausalChainValidator.Validate(baseServerRevision, predecessorOperationId);

        OperationId = operationId;
        ProductId = productId;
        Description = trimmedDescription;
        Price = price;
        BaseServerRevision = baseServerRevision;
        PredecessorOperationId = predecessorOperationId;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid OperationId { get; }

    public Guid ProductId { get; }

    public SyncOperationKind Kind => SyncOperationKind.UpdateProductDetails;

    public string Description { get; }

    public decimal Price { get; }

    public long? BaseServerRevision { get; }

    public Guid? PredecessorOperationId { get; }

    public DateTimeOffset CreatedAtUtc { get; }

    public static UpdateProductDetailsOperation BasedOnServer(
        Guid operationId,
        Guid productId,
        string description,
        decimal price,
        long baseServerRevision,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, description, price, baseServerRevision, null, createdAtUtc);

    public static UpdateProductDetailsOperation Chained(
        Guid operationId,
        Guid productId,
        string description,
        decimal price,
        Guid predecessorOperationId,
        DateTimeOffset createdAtUtc) =>
        new(operationId, productId, description, price, null, predecessorOperationId, createdAtUtc);
}
