namespace RetailLab.Core;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    internal Order(string customerIdentifier, DateTimeOffset placedAtUtc)
        : this(Guid.NewGuid(), customerIdentifier, placedAtUtc)
    {
    }

    /// <summary>
    /// Creates an order with a prescribed id. Web checkout passes its
    /// server-generated checkout-attempt identifier here, so a repeated
    /// submission resolves to the original order instead of creating another.
    /// </summary>
    internal Order(Guid id, string customerIdentifier, DateTimeOffset placedAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("An order id must not be empty.", nameof(id));
        }

        Id = id;
        CustomerIdentifier = global::RetailLab.Core.CustomerIdentifier.Normalize(customerIdentifier);
        PlacedAtUtc = placedAtUtc.ToUniversalTime();
    }

    public Guid Id { get; private set; }

    public string CustomerIdentifier { get; private set; } = string.Empty;

    public DateTimeOffset PlacedAtUtc { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines;

    public decimal Total => _lines.Sum(line => line.LineTotal);

    internal void AddLine(Product product, int quantity)
    {
        _lines.Add(new OrderLine(Id, product, quantity));
    }
}
