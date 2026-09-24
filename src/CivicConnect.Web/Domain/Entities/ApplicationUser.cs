using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Web.Domain.Entities;

public class ApplicationUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
}
