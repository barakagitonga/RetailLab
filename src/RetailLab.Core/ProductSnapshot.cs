namespace RetailLab.Core;

/// <summary>
/// Authoritative immutable snapshot of a product's state at a specific server revision.
/// </summary>
public sealed record ProductSnapshot
{
    public ProductSnapshot(
        Guid id,
        string sku,
        string description,
        decimal price,
        int stockQuantity,
        bool isArchived,
        DateTimeOffset? archivedAtUtc,
        long serverRevision)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sku, nameof(sku));
        ArgumentException.ThrowIfNullOrWhiteSpace(description, nameof(description));

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        if (stockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stockQuantity),
                "Stock quantity cannot be negative.");
        }

        if (serverRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serverRevision),
                "Server revision cannot be negative.");
        }

        Id = id;
        Sku = sku.Trim();
        Description = description.Trim();
        Price = price;
        StockQuantity = stockQuantity;
        IsArchived = isArchived;
        ArchivedAtUtc = archivedAtUtc?.ToUniversalTime();
        ServerRevision = serverRevision;
    }

    public Guid Id { get; init; }

    public string Sku { get; init; }

    public string Description { get; init; }

    public decimal Price { get; init; }

    public int StockQuantity { get; init; }

    public bool IsArchived { get; init; }

    public DateTimeOffset? ArchivedAtUtc { get; init; }

    public long ServerRevision { get; init; }

    public static ProductSnapshot FromProduct(Product product)
    {
        ArgumentNullException.ThrowIfNull(product);

        return new ProductSnapshot(
            product.Id,
            product.Sku,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.IsArchived,
            product.ArchivedAtUtc,
            product.ServerRevision);
    }
}
