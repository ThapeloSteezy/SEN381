using System.Security.Claims;

namespace CivicConnect.Web;

public static class UserRoles
{
    public const string Requester = "Requester";
    public const string Staff = "Staff";
    public const string Supervisor = "Supervisor";
    public const string Workers = "Staff,Supervisor";

    public static bool IsSupervisor(this ClaimsPrincipal user) => user.IsInRole(Supervisor);
    public static bool IsStaff(this ClaimsPrincipal user) => user.IsInRole(Staff);

    public static string DisplayName(this ClaimsPrincipal user)
        => user.FindFirstValue(ClaimTypes.GivenName) ?? user.Identity?.Name ?? string.Empty;

    public static string FirstName(this ClaimsPrincipal user)
        => user.DisplayName().Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? string.Empty;
}
