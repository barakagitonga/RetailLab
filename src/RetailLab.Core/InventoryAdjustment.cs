namespace RetailLab.Core;

public sealed class InventoryAdjustment
{
    public const int MaximumReasonLength = 200;
    public const int MaximumActorLength = 100;

    private InventoryAdjustment()
    {
    }

    public InventoryAdjustment(
        Product product,
        int quantityChange,
        int resultingQuantity,
        string reason,
        string actorIdentifier,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantityChange == 0)
        {
            throw new BusinessRuleException("Stock adjustment must not be zero.");
        }

        if (resultingQuantity < 0)
        {
            throw new BusinessRuleException(
                $"Adjustment would leave {product.Sku} with negative stock.");
        }

        Id = Guid.NewGuid();
        ProductId = product.Id;
        Product = product;
        QuantityChange = quantityChange;
        ResultingQuantity = resultingQuantity;
        Reason = NormalizeRequired(reason, MaximumReasonLength, nameof(reason));
        ActorIdentifier = NormalizeRequired(actorIdentifier, MaximumActorLength, nameof(actorIdentifier));
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public Guid ProductId { get; private set; }

    public Product Product { get; private set; } = null!;

    public int QuantityChange { get; private set; }

    public int ResultingQuantity { get; private set; }

    public string Reason { get; private set; } = string.Empty;

    public string ActorIdentifier { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; private set; }

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
