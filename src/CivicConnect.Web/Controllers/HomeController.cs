using System.Security.Claims;
using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRequestService _requestService;

    public HomeController(IRequestService requestService) => _requestService = requestService;

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrEmpty(userId))
            return View(Array.Empty<ServiceRequest>());

        var requests = await _requestService.ListVisibleAsync(userId, User.IsSupervisor(), cancellationToken);
        return View(requests);
    }

    public IActionResult Error(int? code)
    {
        ViewBag.Code = code;
        return View();
    }
}
