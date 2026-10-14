using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

public class AccountController : Controller
{
    private readonly MongoUserDirectory _users;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher = new();

    public AccountController(MongoUserDirectory users) => _users = users;

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return Redirect(SafeReturnUrl(returnUrl));

        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpGet]
    public IActionResult AccessDenied() => View();

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var user = await _users.FindByEmailAsync(model.Email, cancellationToken);
        var passwordResult = user is null
            ? PasswordVerificationResult.Failed
            : _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (user is null || passwordResult == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(string.Empty, "Incorrect email or password.");
            model.Password = string.Empty;
            return View(model);
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Email),
            new(ClaimTypes.Email, user.Email),
            new(ClaimTypes.GivenName, string.IsNullOrWhiteSpace(user.DisplayName) ? user.Email : user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity),
            new AuthenticationProperties { IsPersistent = model.RememberMe });

        return Redirect(SafeReturnUrl(model.ReturnUrl));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [Authorize]
    [HttpGet]
    public IActionResult Password() => View(new ChangePasswordViewModel());

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Password(ChangePasswordViewModel model, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty, cancellationToken);
        if (user is null)
            return Challenge();

        if (ModelState.IsValid
            && _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.CurrentPassword) == PasswordVerificationResult.Failed)
            ModelState.AddModelError(nameof(model.CurrentPassword), "That is not your current password.");

        if (!ModelState.IsValid)
            return View(new ChangePasswordViewModel());

        await _users.SetPasswordHashAsync(user.Id, _passwordHasher.HashPassword(user, model.NewPassword), cancellationToken);
        TempData["Success"] = "Your password is changed.";
        return RedirectToAction("Index", "Home");
    }

    // Only local GET pages are valid destinations; status changes are POST-only.
    private string SafeReturnUrl(string? returnUrl)
        => !string.IsNullOrEmpty(returnUrl)
           && Url.IsLocalUrl(returnUrl)
           && !returnUrl.Contains("/ChangeStatus", StringComparison.OrdinalIgnoreCase)
            ? returnUrl
            : Url.Action("Index", "Home")!;
}

public sealed class ChangePasswordViewModel
{
    [Required(ErrorMessage = "Enter your current password.")]
    [DataType(DataType.Password)]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter a new password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    [DataType(DataType.Password)]
    public string NewPassword { get; set; } = string.Empty;

    [Compare(nameof(NewPassword), ErrorMessage = "The two new passwords are not the same.")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public sealed class LoginViewModel
{
    [Required(ErrorMessage = "Enter your email.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter your password.")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}
