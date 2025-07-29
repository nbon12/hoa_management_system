using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace HOAManagementCompany.Controllers;

[ApiController]
[Route("api/Identity/Account")]
public class AccountController : ControllerBase
{
    private readonly SignInManager<IdentityUser> _signInManager;

    public AccountController(SignInManager<IdentityUser> signInManager)
    {
        _signInManager = signInManager;
    }

    [HttpPost("Login")]
    public async Task<IActionResult> Login([FromForm] string email, [FromForm] string password, [FromForm] bool rememberMe = false, [FromForm] string? returnUrl = null)
    {
        var result = await _signInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false);
        
        if (result.Succeeded)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }
            return Redirect("/");
        }
        
        return RedirectToAction("Login", "Account", new { error = "Invalid login attempt" });
    }

    [HttpPost("Logout")]
    public async Task<IActionResult> Logout()
    {
        await _signInManager.SignOutAsync();
        return Redirect("/");
    }
} 