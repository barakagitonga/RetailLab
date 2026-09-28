using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class IndexModel(CatalogService catalog, CatalogDisplayMapper mapper) : PageModel
{
    public IReadOnlyList<ProductSummary> Preview { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var active = await catalog.GetActiveProductsAsync();
        Preview = active.Take(3).Select(mapper.ToSummary).ToList();
    }
}
