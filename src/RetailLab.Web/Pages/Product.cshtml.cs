using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages;

public class ProductModel(
    CatalogService catalog,
    CatalogDisplayMapper mapper,
    ProductImageResolver images,
    BookmarkService bookmarks,
    UserManager<IdentityUser> users) : PageModel
{
    public ProductDetails? Product { get; private set; }

    public bool IsFavourite { get; private set; }

    public string? ImageUrl { get; private set; }

    public bool HasImage => ImageUrl is not null;

    public IReadOnlyList<DemoAccordion> DemoSections { get; private set; } = [];

    public IReadOnlyList<DemoReview> DemoReviews { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync(string? sku)
    {
        var product = await catalog.FindActiveProductAsync(sku ?? string.Empty);
        if (product is null)
        {
            return NotFound();
        }

        Product = mapper.ToDetails(product);
        ImageUrl = images.Resolve(product.Sku);

        if (DemoProductContent.AccordionSectionsBySku.TryGetValue(product.Sku, out var sections))
        {
            DemoSections = sections.Select(section => new DemoAccordion { Title = section.Title, Body = section.Body }).ToList();
        }
        else
        {
            DemoSections = [];
        }

        if (DemoProductContent.ReviewsBySku.TryGetValue(product.Sku, out var reviews))
        {
            DemoReviews = reviews.Select(review => new DemoReview { Text = review }).ToList();
        }

        var customerId = users.GetUserId(User);
        if (customerId is not null)
        {
            var saved = await bookmarks.GetBookmarksAsync(customerId);
            IsFavourite = saved.Any(bookmark =>
                string.Equals(bookmark.Product.Sku, product.Sku, StringComparison.OrdinalIgnoreCase));
        }

        return Page();
    }
}
