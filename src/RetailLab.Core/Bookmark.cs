namespace RetailLab.Core;

public sealed class Bookmark
{
    private Bookmark()
    {
    }

    public Bookmark(string customerIdentifier, Product product, DateTimeOffset createdAtUtc)
    {
        ArgumentNullException.ThrowIfNull(product);

        CustomerIdentifier = global::RetailLab.Core.CustomerIdentifier.Normalize(customerIdentifier);
        ProductId = product.Id;
        Product = product;
        CreatedAtUtc = createdAtUtc.ToUniversalTime();
    }

    public string CustomerIdentifier { get; private set; } = string.Empty;

    public Guid ProductId { get; private set; }

    public DateTimeOffset CreatedAtUtc { get; private set; }

    public Product Product { get; private set; } = null!;
}
