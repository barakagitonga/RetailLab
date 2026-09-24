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
    }

    public Guid Id { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string Description { get; private set; } = string.Empty;

    public decimal Price { get; private set; }

    public int StockQuantity { get; private set; }

    public void ReduceStock(int quantity)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be greater than zero.");
        }

        if (quantity > StockQuantity)
        {
            throw new BusinessRuleException(
                $"Insufficient stock for {Sku}. Requested {quantity}, but only {StockQuantity} available.");
        }

        StockQuantity -= quantity;
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
