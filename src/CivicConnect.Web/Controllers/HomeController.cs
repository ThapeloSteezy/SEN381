using Microsoft.AspNetCore.Mvc;

namespace CivicConnect.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index() => View();

    public IActionResult Error() => View();
}
