namespace RetailLab.Core;

public sealed class ProductService(IRetailRepository repository, TimeProvider timeProvider)
{
    public async Task<Product> CreateAsync(
        string sku,
        string description,
        decimal price,
        int stockQuantity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var existing = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (existing is not null)
        {
            throw new BusinessRuleException($"A product with SKU '{sku.Trim()}' already exists.");
        }

        var product = new Product(sku, description, price, stockQuantity);
        repository.AddProduct(product);
        await repository.SaveProductChangesAsync(cancellationToken);

        return product;
    }

    public Task<Product> UpdateDetailsAsync(
        string sku,
        string description,
        decimal price,
        CancellationToken cancellationToken = default)
    {
        return UpdateDetailsCoreAsync(sku, description, price, expectedVersion: null, cancellationToken);
    }

    /// <summary>
    /// Updates details only when the product still carries the version the
    /// caller saw. A caller holding a displayed row or an open form passes the
    /// version it captured; a mismatch means another interface saved first, so
    /// the stale values are rejected with <see cref="ProductConflictException" />
    /// before any mutation or save. Races after this check still fail at the
    /// save-time concurrency token.
    /// </summary>
    public Task<Product> UpdateDetailsAsync(
        string sku,
        string description,
        decimal price,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return UpdateDetailsCoreAsync(sku, description, price, expectedVersion, cancellationToken);
    }

    public Task<Product> ArchiveAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        return ArchiveCoreAsync(sku, expectedVersion: null, cancellationToken);
    }

    /// <summary>
    /// Archives only when the product still carries the version the caller
    /// saw; see <see cref="UpdateDetailsAsync(string, string, decimal, int, CancellationToken)" />
    /// for why callers with displayed state pass an expected version.
    /// </summary>
    public Task<Product> ArchiveAsync(
        string sku,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return ArchiveCoreAsync(sku, expectedVersion, cancellationToken);
    }

    public Task<Product> UnarchiveAsync(
        string sku,
        CancellationToken cancellationToken = default)
    {
        return UnarchiveCoreAsync(sku, expectedVersion: null, cancellationToken);
    }

    /// <summary>
    /// Unarchives only when the product still carries the version the caller
    /// saw; see <see cref="UpdateDetailsAsync(string, string, decimal, int, CancellationToken)" />
    /// for why callers with displayed state pass an expected version.
    /// </summary>
    public Task<Product> UnarchiveAsync(
        string sku,
        int expectedVersion,
        CancellationToken cancellationToken = default)
    {
        return UnarchiveCoreAsync(sku, expectedVersion, cancellationToken);
    }

    private async Task<Product> UpdateDetailsCoreAsync(
        string sku,
        string description,
        decimal price,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var product = await FindOrThrowAsync(sku, expectedVersion, cancellationToken);

        product.UpdateDetails(description, price);
        await repository.SaveProductChangesAsync(cancellationToken);

        return product;
    }

    private async Task<Product> ArchiveCoreAsync(
        string sku,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var product = await FindOrThrowAsync(sku, expectedVersion, cancellationToken);

        product.Archive(timeProvider.GetUtcNow());
        await repository.SaveProductChangesAsync(cancellationToken);

        return product;
    }

    private async Task<Product> UnarchiveCoreAsync(
        string sku,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        var product = await FindOrThrowAsync(sku, expectedVersion, cancellationToken);

        product.Unarchive();
        await repository.SaveProductChangesAsync(cancellationToken);

        return product;
    }

    private async Task<Product> FindOrThrowAsync(
        string sku,
        int? expectedVersion,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);

        var product = await repository.FindProductBySkuAsync(sku.Trim(), cancellationToken);
        if (product is null)
        {
            throw new BusinessRuleException($"No product with SKU '{sku.Trim()}' was found.");
        }

        if (expectedVersion.HasValue && product.Version != expectedVersion.Value)
        {
            // The caller's displayed state predates a save from another
            // interface. Reject before mutating so the winner survives.
            throw new ProductConflictException();
        }

        return product;
    }
}
