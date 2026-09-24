namespace RetailLab.Core;

public sealed class Order
{
    private readonly List<OrderLine> _lines = [];

    private Order()
    {
    }

    internal Order(string customerIdentifier, DateTimeOffset placedAtUtc)
    {
        Id = Guid.NewGuid();
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
