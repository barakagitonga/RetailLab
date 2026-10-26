using RetailLab.Core;

namespace RetailLab.Tests;

public sealed class InventoryAdjustmentTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 22, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Constructor_WithPrescribedId_PreservesOperationIdentity()
    {
        var id = Guid.NewGuid();
        var product = new Product("LEDGER-01", "Ledger product", 10m, 5);

        var adjustment = new InventoryAdjustment(
            id,
            product,
            quantityChange: 2,
            resultingQuantity: 7,
            reason: SynchronizedAdjustmentReasons.Restock,
            actorIdentifier: "sync-device:device-01",
            createdAtUtc: Now);

        Assert.Equal(id, adjustment.Id);
        Assert.Equal(product.Id, adjustment.ProductId);
        Assert.Equal(2, adjustment.QuantityChange);
        Assert.Equal(7, adjustment.ResultingQuantity);
    }

    [Fact]
    public void Constructor_WithPrescribedId_RejectsEmptyId()
    {
        var product = new Product("LEDGER-02", "Ledger product", 10m, 5);

        Assert.Throws<ArgumentException>(() => new InventoryAdjustment(
            Guid.Empty,
            product,
            quantityChange: 2,
            resultingQuantity: 7,
            reason: SynchronizedAdjustmentReasons.Restock,
            actorIdentifier: "sync-device:device-01",
            createdAtUtc: Now));
    }
}
