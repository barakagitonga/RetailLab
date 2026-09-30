using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using RetailLab.Core;
using RetailLab.Web.Models;
using RetailLab.Web.Services;

namespace RetailLab.Web.Pages.Orders;

[Authorize]
public class DetailsModel(
    CheckoutService checkout,
    CatalogDisplayMapper mapper,
    UserManager<IdentityUser> users) : PageModel
{
    public OrderDetails? Order { get; private set; }

    public async Task<IActionResult> OnGetAsync(Guid? id)
    {
        var customerId = users.GetUserId(User);
        if (customerId is null)
        {
            return Challenge();
        }

        if (id is null)
        {
            return NotFound();
        }

        // Customer-scoped: a forged id for another customer's order reads as
        // missing, so it renders the same 404 without leaking anything.
        var order = await checkout.GetOrderAsync(customerId, id.Value);
        if (order is null)
        {
            return NotFound();
        }

        Order = mapper.ToOrderDetails(order);
        return Page();
    }
}
