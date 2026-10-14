using System.Security.Claims;
using CivicConnect.Web.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.ViewComponents;

public sealed class UpdatesLinkViewComponent : ViewComponent
{
    private readonly IRequestService _requestService;

    public UpdatesLinkViewComponent(IRequestService requestService) => _requestService = requestService;

    public async Task<IViewComponentResult> InvokeAsync(bool current)
    {
        var userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        var unread = string.IsNullOrEmpty(userId) ? 0 : await _requestService.CountUnreadUpdatesAsync(userId, HttpContext.RequestAborted);
        ViewData["Current"] = current;
        return View(unread);
    }
}
