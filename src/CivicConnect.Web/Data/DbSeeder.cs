using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Web.Data;

public static class DbSeeder
{
    private static readonly string[] Roles =
    [
        "Requester",
        "Staff",
        "Supervisor",
        "Management",
        "Admin"
    ];

    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        await db.Database.EnsureCreatedAsync();

        foreach (var role in Roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
                await roleManager.CreateAsync(new IdentityRole(role));
        }

        var categories = new[]
        {
            "Facility Fault",
            "Damaged Equipment",
            "Security Concern",
            "IT Support",
            "Maintenance",
            "Lost Property"
        };

        foreach (var category in categories)
        {
            if (!await db.RequestCategories.AnyAsync(c => c.Name == category))
                db.RequestCategories.Add(new RequestCategory { Name = category });
        }

        await db.SaveChangesAsync();

        if (!configuration.GetValue<bool>("CivicConnect:SeedDemoUsers"))
            return;

        var demoPassword = configuration["CivicConnect:DemoPassword"];
        if (string.IsNullOrWhiteSpace(demoPassword))
            throw new InvalidOperationException("CivicConnect:DemoPassword is required when demo-user seeding is enabled.");

        await EnsureUserAsync(userManager, "requester@civicconnect.local", "Requester", "Requester Demo", demoPassword);
        await EnsureUserAsync(userManager, "staff@civicconnect.local", "Staff", "Staff Demo", demoPassword);
        await EnsureUserAsync(userManager, "supervisor@civicconnect.local", "Supervisor", "Supervisor Demo", demoPassword);
        await EnsureUserAsync(userManager, "management@civicconnect.local", "Management", "Management Demo", demoPassword);
    }

    private static async Task EnsureUserAsync(
        UserManager<ApplicationUser> userManager,
        string email,
        string role,
        string displayName,
        string password)
    {
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName
            };

            var result = await userManager.CreateAsync(user, password);
            if (!result.Succeeded)
                throw new InvalidOperationException(string.Join("; ", result.Errors.Select(e => e.Description)));
        }

        if (!await userManager.IsInRoleAsync(user, role))
            await userManager.AddToRoleAsync(user, role);
    }
}
