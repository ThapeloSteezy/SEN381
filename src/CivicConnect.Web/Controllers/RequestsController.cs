using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly IRequestRepository _repository;
    private readonly IRequestService _requestService;
    private readonly UserManager<CivicConnect.Web.Domain.Entities.ApplicationUser> _userManager;

    public RequestsController(
        IRequestRepository repository,
        IRequestService requestService,
        UserManager<CivicConnect.Web.Domain.Entities.ApplicationUser> userManager)
    {
        _repository = repository;
        _requestService = requestService;
        _userManager = userManager;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var staffAccess = User.IsInRole("Staff") || User.IsInRole("Supervisor") ||
                          User.IsInRole("Management") || User.IsInRole("Admin");

        var requests = await _repository.GetVisibleToUserAsync(user.Id, staffAccess, cancellationToken);
        return View(requests);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _repository.GetActiveCategoriesAsync(cancellationToken);
        return View(new CreateRequestViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Categories = await _repository.GetActiveCategoriesAsync(cancellationToken);
            return View(model);
        }

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        try
        {
            var request = await _requestService.CreateAsync(
                user.Id,
                model.Title,
                model.Description,
                model.CategoryId,
                model.Priority,
                cancellationToken);

            return RedirectToAction(nameof(Details), new { id = request.Id });
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            ViewBag.Categories = await _repository.GetActiveCategoriesAsync(cancellationToken);
            return View(model);
        }
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        var request = await _repository.GetByIdAsync(id, cancellationToken);
        if (request is null) return NotFound();

        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var staffAccess = User.IsInRole("Staff") || User.IsInRole("Supervisor") ||
                          User.IsInRole("Management") || User.IsInRole("Admin");

        if (!staffAccess && request.SubmittedById != user.Id)
            return Forbid();

        return View(request);
    }

    [Authorize(Roles = "Staff,Supervisor,Admin")]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(Guid id, RequestStatus newStatus, string? notes, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await _requestService.ChangeStatusAsync(
            id, newStatus, user.Id, notes, cancellationToken);

        if (!result.Success)
            TempData["Error"] = result.Error;
        else
            TempData["Success"] = "Request status updated and history/feedback recorded.";

        return RedirectToAction(nameof(Details), new { id });
    }
}

public sealed class CreateRequestViewModel
{
    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(120)]
    public string Title { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Required]
    [System.ComponentModel.DataAnnotations.StringLength(4000, MinimumLength = 10)]
    public string Description { get; set; } = string.Empty;

    [System.ComponentModel.DataAnnotations.Range(1, int.MaxValue)]
    public int CategoryId { get; set; }

    public RequestPriority Priority { get; set; } = RequestPriority.Medium;
}
