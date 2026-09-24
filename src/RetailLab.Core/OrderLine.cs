namespace RetailLab.Core;

public sealed class OrderLine
{
    private OrderLine()
    {
    }

    internal OrderLine(Guid orderId, Product product, int quantity)
    {
        ArgumentNullException.ThrowIfNull(product);

        if (quantity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity), "Order quantity must be greater than zero.");
        }

        Id = Guid.NewGuid();
        OrderId = orderId;
        ProductId = product.Id;
        Sku = product.Sku;
        ProductDescription = product.Description;
        UnitPrice = product.Price;
        Quantity = quantity;
    }

    public Guid Id { get; private set; }

    public Guid OrderId { get; private set; }

    public Guid ProductId { get; private set; }

    public string Sku { get; private set; } = string.Empty;

    public string ProductDescription { get; private set; } = string.Empty;

    public decimal UnitPrice { get; private set; }

    public int Quantity { get; private set; }

    public decimal LineTotal => UnitPrice * Quantity;
}
