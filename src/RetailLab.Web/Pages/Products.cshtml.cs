using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class ProductsModel(CatalogService catalog, CatalogDisplayMapper mapper) : PageModel
{
    public IReadOnlyList<ProductSummary> Products { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var active = await catalog.GetActiveProductsAsync();
        Products = active.Select(mapper.ToSummary).ToList();
    }
}
