using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class ProductModel(CatalogService catalog, CatalogDisplayMapper mapper) : PageModel
{
    public ProductDetails? Product { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? sku)
    {
        var product = await catalog.FindActiveProductAsync(sku ?? string.Empty);
        if (product is null)
        {
            return NotFound();
        }

        Product = mapper.ToDetails(product);
        return Page();
    }
}
