namespace RetailLab.Core;

public sealed class Product
{
    public const int MaximumSkuLength = 40;
    public const int MaximumDescriptionLength = 300;

    private Product()
    {
    }

    public Product(string sku, string description, decimal price, int stockQuantity)
        : this(Guid.NewGuid(), sku, description, price, stockQuantity)
    {
    }

    public Product(Guid id, string sku, string description, decimal price, int stockQuantity)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Product ID cannot be empty.", nameof(id));
        }

        Id = id;
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
        ServerRevision = 0;
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

    /// <summary>
    /// Authoritative server revision representing the latest server state
    /// observed or applied. Not an EF concurrency token.
    /// </summary>
    public long ServerRevision { get; private set; }

    public bool IsArchived { get; private set; }

    public DateTimeOffset? ArchivedAtUtc { get; private set; }

    /// <summary>
    /// Creates a new local projection from the first authoritative server snapshot.
    /// The local concurrency version starts at zero because this is a local insert,
    /// while ServerRevision records the server's independent revision.
    /// </summary>
    public static Product CreateFromServerSnapshot(ServerChangeEntry entry)
    {
        var values = ValidateServerSnapshot(entry);
        if (entry.ServerRevision != 1)
        {
            throw new BusinessRuleException(
                $"A new synchronized product must start at server revision 1, not {entry.ServerRevision}.");
        }

        var product = new Product(
            values.Id,
            values.Sku,
            values.Description,
            values.Price,
            values.StockQuantity)
        {
            IsArchived = values.IsArchived,
            ArchivedAtUtc = values.ArchivedAtUtc
        };

        product.ApplyServerRevision(values.ServerRevision);
        return product;
    }

    /// <summary>
    /// Replaces an existing local projection with the next authoritative snapshot.
    /// Validation completes before mutation so a rejected snapshot changes nothing.
    /// </summary>
    public void ApplyServerSnapshot(ServerChangeEntry entry)
    {
        var values = ValidateServerSnapshot(entry);
        if (values.Id != Id)
        {
            throw new BusinessRuleException(
                $"Server snapshot product {values.Id} cannot be applied to local product {Id}.");
        }

        var expectedRevision = checked(ServerRevision + 1);
        if (values.ServerRevision != expectedRevision)
        {
            throw new BusinessRuleException(
                $"Expected server revision {expectedRevision}, but received {values.ServerRevision}.");
        }

        var nextVersion = checked(Version + 1);

        Sku = values.Sku;
        Description = values.Description;
        Price = values.Price;
        StockQuantity = values.StockQuantity;
        IsArchived = values.IsArchived;
        ArchivedAtUtc = values.ArchivedAtUtc;
        ServerRevision = values.ServerRevision;
        Version = nextVersion;
    }

    /// <summary>
    /// Applies an authoritative server revision. Rejects negative revisions
    /// and revision regression. Does not alter local Version.
    /// </summary>
    public void ApplyServerRevision(long serverRevision)
    {
        if (serverRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(serverRevision),
                "Server revision cannot be negative.");
        }

        if (serverRevision < ServerRevision)
        {
            throw new BusinessRuleException(
                $"Authoritative server revision cannot regress from {ServerRevision} to {serverRevision}.");
        }

        ServerRevision = serverRevision;
    }

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

    private static ValidatedServerSnapshot ValidateServerSnapshot(ServerChangeEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        ArgumentNullException.ThrowIfNull(entry.Snapshot);

        var snapshot = entry.Snapshot;
        if (entry.ProductId != snapshot.Id)
        {
            throw new BusinessRuleException("The feed entry product ID does not match its snapshot ID.");
        }

        if (entry.ServerRevision != snapshot.ServerRevision)
        {
            throw new BusinessRuleException("The feed entry revision does not match its snapshot revision.");
        }

        if (entry.OccurredAtUtc == default)
        {
            throw new BusinessRuleException("The feed entry occurrence timestamp is required.");
        }

        if (snapshot.IsArchived != snapshot.ArchivedAtUtc.HasValue)
        {
            throw new BusinessRuleException(
                "Archived products require an archive timestamp, and active products must not have one.");
        }

        var sku = NormalizeRequired(snapshot.Sku, MaximumSkuLength, nameof(snapshot.Sku));
        var description = NormalizeRequired(
            snapshot.Description,
            MaximumDescriptionLength,
            nameof(snapshot.Description));

        if (snapshot.Price < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot.Price), "Price cannot be negative.");
        }

        if (snapshot.StockQuantity < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot.StockQuantity),
                "Stock quantity cannot be negative.");
        }

        if (snapshot.ServerRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(snapshot.ServerRevision),
                "Server revision cannot be negative.");
        }

        return new ValidatedServerSnapshot(
            snapshot.Id,
            sku,
            description,
            snapshot.Price,
            snapshot.StockQuantity,
            snapshot.IsArchived,
            snapshot.ArchivedAtUtc?.ToUniversalTime(),
            snapshot.ServerRevision);
    }

    private sealed record ValidatedServerSnapshot(
        Guid Id,
        string Sku,
        string Description,
        decimal Price,
        int StockQuantity,
        bool IsArchived,
        DateTimeOffset? ArchivedAtUtc,
        long ServerRevision);
}
