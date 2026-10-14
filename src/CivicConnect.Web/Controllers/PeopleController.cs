using System.ComponentModel.DataAnnotations;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;

namespace CivicConnect.Web.Controllers;

[Authorize(Roles = UserRoles.Supervisor)]
public class PeopleController : Controller
{
    private readonly MongoUserDirectory _users;
    private readonly PasswordHasher<ApplicationUser> _passwordHasher = new();

    public PeopleController(MongoUserDirectory users) => _users = users;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
        => View(new PeopleViewModel(await _users.ListAllAsync(cancellationToken), new AddPersonViewModel()));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Add([Bind(Prefix = "NewPerson")] AddPersonViewModel model, CancellationToken cancellationToken)
    {
        if (ModelState.IsValid && await _users.FindByEmailAsync(model.Email, cancellationToken) is not null)
            ModelState.AddModelError("NewPerson.Email", "Someone already uses this email.");

        if (!ModelState.IsValid)
            return View(nameof(Index), new PeopleViewModel(await _users.ListAllAsync(cancellationToken), model));

        var user = new ApplicationUser
        {
            Email = model.Email.Trim().ToLowerInvariant(),
            DisplayName = model.Name.Trim(),
            Role = model.Role
        };
        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        try
        {
            await _users.InsertAsync(user, cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            ModelState.AddModelError("NewPerson.Email", "Someone already uses this email.");
            return View(nameof(Index), new PeopleViewModel(await _users.ListAllAsync(cancellationToken), model));
        }

        TempData["Success"] = $"Added {user.DisplayName}. Give them their email and password.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id, string? password, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(id, cancellationToken);
        if (user is null)
        {
            TempData["Error"] = "That person no longer exists.";
        }
        else if (string.IsNullOrWhiteSpace(password) || password.Length < 8)
        {
            TempData["Error"] = $"Password for {user.DisplayName} not changed. Use at least 8 characters.";
        }
        else
        {
            await _users.SetPasswordHashAsync(user.Id, _passwordHasher.HashPassword(user, password), cancellationToken);
            TempData["Success"] = $"New password set for {user.DisplayName}.";
        }

        return RedirectToAction(nameof(Index));
    }
}

public sealed record PeopleViewModel(IReadOnlyList<ApplicationUser> People, AddPersonViewModel NewPerson);

public sealed class AddPersonViewModel
{
    [Required(ErrorMessage = "Enter their name.")]
    [StringLength(80)]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Enter their email.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression($"^({UserRoles.Requester}|{UserRoles.Staff}|{UserRoles.Supervisor})$", ErrorMessage = "Pick a role.")]
    public string Role { get; set; } = UserRoles.Requester;

    [Required(ErrorMessage = "Give them a password.")]
    [StringLength(100, MinimumLength = 8, ErrorMessage = "Use at least 8 characters.")]
    public string Password { get; set; } = string.Empty;
}
