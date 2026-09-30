namespace RetailLab.Core;

public sealed class Product
{
    public const int MaximumSkuLength = 40;
    public const int MaximumDescriptionLength = 300;

    private Product()
    {
    }

    public Product(string sku, string description, decimal price, int stockQuantity)
    {
        Id = Guid.NewGuid();
        Sku = NormalizeRequired(sku, MaximumSkuLength, nameof(sku));
        Description = NormalizeRequired(description, MaximumDescriptionLength, nameof(description));

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

        Price = price;
        StockQuantity = stockQuantity;
        IsArchived = false;
        ArchivedAtUtc = null;
        Version = 0;
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token. Every successful mutation bumps it, so a
    /// stale writer (checkout, staff edit, stock adjustment) loses its update
    /// instead of silently overwriting another request.
    /// </summary>
    public int Version { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    public void UpdateDetails(string description, decimal price)
    {
        if (IsArchived)
        {
            throw new BusinessRuleException(
                $"Product {Sku} is archived. Unarchive it before updating details.");
        }

        if (price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.");
        }

        Description = NormalizeRequired(description, MaximumDescriptionLength, nameof(description));
        Price = price;
        Version = checked(Version + 1);
    }

    public void Archive(DateTimeOffset archivedAtUtc)
    {
        if (IsArchived)
        {
            throw new BusinessRuleException($"Product {Sku} is already archived.");
        }

        IsArchived = true;
        ArchivedAtUtc = archivedAtUtc.ToUniversalTime();
        Version = checked(Version + 1);
    }

    public void Unarchive()
    {
        if (!IsArchived)
        {
            throw new BusinessRuleException($"Product {Sku} is not archived.");
        }

        IsArchived = false;
        ArchivedAtUtc = null;
        Version = checked(Version + 1);
    }

    public void AdjustStock(int delta)
    {
        if (delta == 0)
        {
            throw new BusinessRuleException("Stock adjustment must not be zero.");
        }

        if (StockQuantity + delta < 0)
        {
            throw new BusinessRuleException(
                $"Insufficient stock for {Sku}. Requested {-delta}, but only {StockQuantity} available.");
        }

        StockQuantity += delta;
        Version = checked(Version + 1);
    }

    public void ReduceStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be greater than zero.");
        }

        AdjustStock(-quantity);
    }

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

        var normalized = value.Trim();
        if (normalized.Length > maximumLength)
        {
            throw new ArgumentException(
                $"{parameterName} cannot exceed {maximumLength} characters.",
                parameterName);
        }

        return normalized;
    }
}
