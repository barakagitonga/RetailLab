using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace RetailLab.Web.Pages.Account;

[AllowAnonymous]
public class LoginModel(SignInManager<IdentityUser> signIn) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public string? ReturnUrl { get; private set; }

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

        // UserName holds the trimmed email (see Register), and Identity
        // compares normalized values, so sign-in is case-insensitive.
        // Failed attempts count toward lockout, per the approved design.
        var result = await signIn.PasswordSignInAsync(
            Input.Email.Trim(),
            Input.Password,
            Input.RememberMe,
            lockoutOnFailure: true);

        if (result.IsLockedOut)
        {
            ModelState.AddModelError(
                string.Empty,
                "This account is temporarily locked. Please try again later.");
            return Page();
        }

        if (!result.Succeeded)
        {
            // Unknown email and wrong password share one message so the
            // response never reveals whether the email exists.
            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return Page();
        }

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
        [DataType(DataType.Password)]
        [Display(Name = "Password")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}
