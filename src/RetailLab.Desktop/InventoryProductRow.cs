using RetailLab.Core;

namespace RetailLab.Desktop;

/// <summary>
/// Staff-facing display data. Keeping this separate from Product means WPF
/// formatting and labels do not become business-domain concerns. Version is an
/// internal concurrency value and is never displayed to staff.
/// </summary>
public sealed record InventoryProductRow(
    string Sku,
    string Description,
    decimal Price,
    int StockQuantity,
    bool IsArchived,
    int Version)
{
    public string State => IsArchived ? "Archived" : "Active";

    public static InventoryProductRow FromProduct(Product product) =>
        new(
            product.Sku,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.IsArchived,
            product.Version);
}
