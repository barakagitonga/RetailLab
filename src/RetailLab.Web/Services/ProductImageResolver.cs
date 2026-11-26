using Microsoft.AspNetCore.Hosting;

namespace RetailLab.Web.Services;

/// <summary>
/// Resolves product images by the SKU-filename convention
/// wwwroot/images/products/{SKU}.jpg (storefront design section 4). No schema
/// field is added; a missing file is "no image" — callers render the honest
/// placeholder tile, never a broken image link. SKU values reach the file
/// provider, so obvious path characters are refused defensively even though
/// catalogue SKUs come from validated business rules.
/// </summary>
public sealed class ProductImageResolver(IWebHostEnvironment environment)
{
    public string? Resolve(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            return null;
        }

        if (sku.Contains('/') || sku.Contains('\\') || sku.Contains(".."))
        {
            return null;
        }

        var fileInfo = environment.WebRootFileProvider.GetFileInfo($"images/products/{sku}.jpg");
        return fileInfo.Exists && fileInfo.Length > 0
            ? $"/images/products/{sku}.jpg"
            : null;
    }
}
