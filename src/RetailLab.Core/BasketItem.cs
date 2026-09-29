namespace RetailLab.Core;

public sealed class BasketItem
{
    private BasketItem()
    {
    }

    public BasketItem(
        string customerIdentifier,
        Product product,
        int quantity,
        DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Basket quantity must be greater than zero.");
        }

        CustomerIdentifier = global::RetailLab.Core.CustomerIdentifier.Normalize(customerIdentifier);
        ProductId = product.Id;
        Product = product;
        Quantity = quantity;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
        UpdatedAtUtc = CreatedAtUtc;
        Version = 0;
    }

    public string CustomerIdentifier { get; private set; } = string.Empty;

    public Guid ProductId { get; private set; }

    public int Quantity { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public DateTimeOffset UpdatedAtUtc { get; private set; }

    /// <summary>
    /// Optimistic-concurrency token. EF Core compares the original value on
    /// every update or delete; a stale writer loses with a persistence
    /// conflict instead of silently overwriting another request.
    /// </summary>
    public int Version { get; private set; }

    public Product Product { get; private set; } = null!;

    public void Increase(int amount, DateTimeOffset updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Increase amount must be greater than zero.");
        }

        Quantity = checked(Quantity + amount);
        Touch(updatedAtUtc);
    }

    public void SetQuantity(int quantity, DateTimeOffset updatedAtUtc)
    {
        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Basket quantity must be greater than zero.");
        }

        Quantity = quantity;
        Touch(updatedAtUtc);
    }

    private void Touch(DateTimeOffset updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc.ToUniversalTime();
        Version = checked(Version + 1);
    }
}
