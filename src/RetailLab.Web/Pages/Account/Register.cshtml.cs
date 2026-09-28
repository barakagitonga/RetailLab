using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RetailLab.Web.Pages.Account;

[AllowAnonymous]
public class RegisterModel(
    UserManager<IdentityUser> users,
    SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

    [TempData]
    public string? StatusMessage { get; set; }

    public IActionResult OnGet(string? returnUrl)
    {
        if (signIn.IsSignedIn(User))
        {
            return RedirectToLocal(returnUrl);
        }

        ReturnUrl = returnUrl;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(string? returnUrl)
    {
        if (signIn.IsSignedIn(User))
        {
            return RedirectToLocal(returnUrl);
        }

        ReturnUrl = returnUrl;

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var email = Input.Email.Trim();
        var user = new IdentityUser
        {
            UserName = email,
            Email = email,
            // No email sender exists in this slice, so registration confirms
            // the address immediately. A later slice can require confirmation.
            EmailConfirmed = true
        };

        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            return Page();
        }

        await signIn.SignInAsync(user, isPersistent: false);
        StatusMessage = "Welcome! Your account is ready.";
        return RedirectToLocal(returnUrl);
    }

    private IActionResult RedirectToLocal(string? returnUrl) =>
        Url.IsLocalUrl(returnUrl) ? LocalRedirect(returnUrl) : RedirectToPage("/Index");

    public sealed class InputModel
    {
        [Required]
        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        [Compare(nameof(Password), ErrorMessage = "The passwords do not match.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
