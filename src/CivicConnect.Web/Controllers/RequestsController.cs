using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

[Authorize]
public class RequestsController : Controller
{
    private readonly IRequestService _requestService;
    private readonly IUserDirectory _users;

    public RequestsController(IRequestService requestService, IUserDirectory users)
    {
        _requestService = requestService;
        _users = users;
    }

    public async Task<IActionResult> Index(RequestStatus? status, string? q, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Challenge();

        var requests = await _requestService.ListVisibleAsync(userId, User.IsSupervisor(), cancellationToken);
        var query = q?.Trim() ?? string.Empty;

        var shown = requests
            .Where(r => status is null || r.Status == status)
            .Where(r => query.Length == 0
                || r.Title.Contains(query, StringComparison.OrdinalIgnoreCase)
                || r.RequestNumber.Contains(query, StringComparison.OrdinalIgnoreCase)
                || FacilityPlan.NameOf(r.RoomId).Contains(query, StringComparison.OrdinalIgnoreCase)
                || (r.Category?.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false)
                || (r.AssignedTo?.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        return View(new RequestListViewModel(requests, shown, status, query, userId));
    }

    [HttpGet]
    public async Task<IActionResult> Create(string? room, CancellationToken cancellationToken)
    {
        ViewBag.Categories = await _requestService.GetActiveCategoriesAsync(cancellationToken);
        return View(new CreateRequestViewModel { RoomId = FacilityPlan.Find(room)?.Id ?? string.Empty });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRequestViewModel model, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out _)) return Challenge();

        if (ModelState.IsValid)
        {
            try
            {
                var request = await _requestService.CreateAsync(
                    Actor(),
                    model.Title ?? string.Empty,
                    model.Description,
                    model.CategoryId,
                    model.RoomId,
                    model.Priority,
                    cancellationToken);

                TempData["Success"] = "Thank you. We got your request.";
                return RedirectToAction(nameof(Details), new { id = request.Id });
            }
            catch (ArgumentException ex)
            {
                ModelState.AddModelError(ex.ParamName switch
                {
                    "categoryId" => nameof(model.CategoryId),
                    "roomId" => nameof(model.RoomId),
                    "description" => nameof(model.Description),
                    _ => string.Empty
                }, ex.Message);
            }
        }

        ViewBag.Categories = await _requestService.GetActiveCategoriesAsync(cancellationToken);
        return View(model);
    }

    public async Task<IActionResult> Details(Guid id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Challenge();

        var request = id == Guid.Empty ? null : await _requestService.GetAsync(id, cancellationToken);

        // Requests the user may not see are reported as missing so their existence is not revealed.
        var allowed = request is not null
            && (User.IsSupervisor() || request.SubmittedById == userId || request.AssignedToId == userId);
        if (!allowed || request is null)
        {
            Response.StatusCode = StatusCodes.Status404NotFound;
            return View("RequestNotFound");
        }

        if (User.IsSupervisor())
            ViewBag.StaffMembers = await _users.ListByRoleAsync(UserRoles.Staff, cancellationToken);

        if (request.Feedback.Any(item => item.RecipientUserId == userId && !item.IsRead))
            await _requestService.MarkUpdatesReadAsync(request.Id, userId, cancellationToken);

        return View(request);
    }

    public async Task<IActionResult> Updates(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Challenge();
        return View(await _requestService.ListUpdatesAsync(userId, cancellationToken));
    }

    [Authorize(Roles = UserRoles.Supervisor)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Assign(
        Guid id,
        RequestStatus currentStatus,
        string? staffId,
        string? notes,
        CancellationToken cancellationToken)
    {
        var result = await _requestService.AssignAsync(id, currentStatus, staffId ?? string.Empty, Actor(), notes, cancellationToken);
        return AfterChange(id, result, "Assigned.", notes);
    }

    [Authorize(Roles = UserRoles.Workers)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(
        Guid id,
        RequestStatus currentStatus,
        RequestStatus newStatus,
        string? notes,
        CancellationToken cancellationToken)
    {
        var result = await _requestService.ChangeStatusAsync(
            id, currentStatus, newStatus, Actor(), User.IsSupervisor(), notes, cancellationToken);
        return AfterChange(id, result, $"Status changed to {newStatus.Label()}.", notes);
    }

    [Authorize(Roles = UserRoles.Supervisor)]
    public async Task<IActionResult> Map(string? room, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId)) return Challenge();

        var requests = await _requestService.ListVisibleAsync(userId, seesAll: true, cancellationToken);
        var byRoom = requests
            .Where(request => request.RoomId is not null)
            .ToLookup(request => request.RoomId!);

        var rooms = FacilityPlan.Rooms
            .Select(facilityRoom =>
            {
                var open = byRoom[facilityRoom.Id].Where(request => request.Status.IsOpen()).ToList();
                return new MapRoomViewModel(
                    facilityRoom,
                    open.Count,
                    byRoom[facilityRoom.Id].Count(),
                    open.Count == 0 ? null : open.Min(request => request.Status));
            })
            .ToList();

        var selected = FacilityPlan.Find(room);
        var selectedRequests = selected is null
            ? []
            : byRoom[selected.Id]
                .OrderBy(request => request.Status.IsOpen() ? 0 : 1)
                .ThenBy(request => request.Status)
                .ThenByDescending(request => request.CreatedAtUtc)
                .ToList();

        return View(new MapViewModel(
            rooms,
            selected,
            selectedRequests,
            UnknownRoomRequested: room is not null && selected is null));
    }

    private RedirectToActionResult AfterChange(Guid id, (bool Success, string? Error) result, string success, string? notes)
    {
        if (result.Success)
        {
            TempData["Success"] = success;
        }
        else
        {
            TempData["Error"] = result.Error;
            TempData["Notes"] = notes;
        }

        return RedirectToAction(nameof(Details), new { id });
    }

    private UserRef Actor() => new()
    {
        Id = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
        Email = User.FindFirstValue(ClaimTypes.Email) ?? string.Empty,
        Name = User.DisplayName()
    };

    private bool TryGetUserId(out string userId)
    {
        userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
        return userId.Length > 0;
    }
}

public sealed class CreateRequestViewModel
{
    [StringLength(120, ErrorMessage = "Keep the name short.")]
    public string? Title { get; set; }

    [Required(ErrorMessage = "Tell us what is wrong.")]
    [StringLength(4000, MinimumLength = 10, ErrorMessage = "Please write a little more.")]
    public string Description { get; set; } = string.Empty;

    [Range(1, int.MaxValue, ErrorMessage = "Pick what kind of problem it is.")]
    public int CategoryId { get; set; }

    [Required(ErrorMessage = "Pick the room.")]
    public string RoomId { get; set; } = string.Empty;

    public RequestPriority Priority { get; set; } = RequestPriority.Medium;
}

public sealed record RequestListViewModel(
    IReadOnlyList<ServiceRequest> All,
    IReadOnlyList<ServiceRequest> Shown,
    RequestStatus? Status,
    string Query,
    string UserId);

public sealed record MapRoomViewModel(FacilityRoom Room, int OpenCount, int TotalCount, RequestStatus? MostUrgentOpenStatus);

public sealed record MapViewModel(
    IReadOnlyList<MapRoomViewModel> Rooms,
    FacilityRoom? SelectedRoom,
    IReadOnlyList<ServiceRequest> SelectedRequests,
    bool UnknownRoomRequested);
