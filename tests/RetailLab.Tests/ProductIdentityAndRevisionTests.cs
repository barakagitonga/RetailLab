using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class ProductIdentityAndRevisionTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 10, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithPrescribedGuid_RetainsId()
    {
        var prescribedId = Guid.NewGuid();
        var product = new Product(prescribedId, "PRE-001", "Prescribed item", 15.00m, 10);

        Assert.Equal(prescribedId, product.Id);
        Assert.Equal("PRE-001", product.Sku);
        Assert.Equal(0, product.Version);
        Assert.Equal(0L, product.ServerRevision);
    }

    [Fact]
    public void Constructor_RejectsEmptyGuid()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => new Product(Guid.Empty, "PRE-001", "Item", 10.00m, 5));

        Assert.Equal("id", ex.ParamName);
    }

    [Fact]
    public void Constructor_WithoutPrescribedGuid_GeneratesNonEmptyId()
    {
        var product = new Product("AUTO-001", "Auto item", 10.00m, 5);

        Assert.NotEqual(Guid.Empty, product.Id);
        Assert.Equal(0, product.Version);
        Assert.Equal(0L, product.ServerRevision);
    }

    [Fact]
    public void LocalMutations_IncrementVersion_WithoutChangingServerRevision()
    {
        var product = new Product("MUT-001", "Mutable item", 10.00m, 5);
        Assert.Equal(0, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.UpdateDetails("Updated mutable item", 12.00m);
        Assert.Equal(1, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.AdjustStock(3);
        Assert.Equal(2, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.ReduceStock(2);
        Assert.Equal(3, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.Archive(Now);
        Assert.Equal(4, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.Unarchive();
        Assert.Equal(5, product.Version);
        Assert.Equal(0L, product.ServerRevision);
    }

    [Fact]
    public void ApplyServerRevision_UpdatesServerRevision_WithoutChangingLocalVersion()
    {
        var product = new Product("REV-001", "Revision item", 10.00m, 5);
        product.UpdateDetails("Updated", 11.00m);
        Assert.Equal(1, product.Version);
        Assert.Equal(0L, product.ServerRevision);

        product.ApplyServerRevision(5L);

        Assert.Equal(1, product.Version); // Version remains unchanged
        Assert.Equal(5L, product.ServerRevision);

        product.ApplyServerRevision(8L);
        Assert.Equal(1, product.Version);
        Assert.Equal(8L, product.ServerRevision);
    }

    [Fact]
    public void ApplyServerRevision_RejectsNegativeRevisionWithoutMutation()
    {
        var product = new Product("REV-001", "Revision item", 10.00m, 5);
        product.ApplyServerRevision(3L);

        var ex = Assert.Throws<ArgumentOutOfRangeException>(
            () => product.ApplyServerRevision(-1L));

        Assert.Equal("serverRevision", ex.ParamName);
        Assert.Equal(3L, product.ServerRevision);
    }

    [Fact]
    public void ApplyServerRevision_RejectsRevisionRegressionWithoutMutation()
    {
        var product = new Product("REV-001", "Revision item", 10.00m, 5);
        product.ApplyServerRevision(10L);

        var ex = Assert.Throws<BusinessRuleException>(
            () => product.ApplyServerRevision(9L));

        Assert.Contains("regress", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(10L, product.ServerRevision);
    }

    [Fact]
    public void ApplyServerRevision_AcceptsSameRevisionIdempotently()
    {
        var product = new Product("REV-001", "Revision item", 10.00m, 5);
        product.ApplyServerRevision(10L);

        product.ApplyServerRevision(10L);

        Assert.Equal(10L, product.ServerRevision);
    }
}
