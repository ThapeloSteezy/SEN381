using System.Text.Json;
using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace CivicConnect.Web.Data;

// Usage: dotnet run --project src/CivicConnect.Web -- import-accounts "<full path to accounts.json>"
// Existing emails are skipped, so the file can be run again after adding people.
public static class AccountImport
{
    private static readonly string[] Roles = [UserRoles.Requester, UserRoles.Staff, UserRoles.Supervisor];
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static async Task<int> RunAsync(IServiceProvider services, string path, TextWriter output)
    {
        var users = services.GetRequiredService<MongoUserDirectory>();
        if (!File.Exists(path))
        {
            output.WriteLine($"File not found: {path}");
            return 1;
        }

        var entries = JsonSerializer.Deserialize<List<AccountEntry>>(
            await File.ReadAllTextAsync(path),
            JsonOptions) ?? [];

        var problems = entries
            .Select((entry, index) => (entry, index))
            .SelectMany(x => Validate(x.entry).Select(problem => $"Entry {x.index + 1} ({x.entry.Email}): {problem}"))
            .ToList();
        var duplicates = entries.GroupBy(e => e.Email.Trim().ToLowerInvariant()).Where(g => g.Count() > 1).Select(g => $"{g.Key} is listed more than once.");
        problems.AddRange(duplicates);
        if (problems.Count > 0)
        {
            problems.ForEach(output.WriteLine);
            output.WriteLine("Nothing was imported.");
            return 1;
        }

        await services.GetRequiredService<MongoContext>().EnsureIndexesAsync();
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var entry in entries)
        {
            var email = entry.Email.Trim().ToLowerInvariant();
            if (await users.FindByEmailAsync(email) is not null)
            {
                output.WriteLine($"Skipped  {email} (already exists)");
                continue;
            }

            var user = new ApplicationUser
            {
                Email = email,
                DisplayName = entry.Name.Trim(),
                Role = Roles.First(r => r.Equals(entry.Role.Trim(), StringComparison.OrdinalIgnoreCase))
            };
            user.PasswordHash = hasher.HashPassword(user, entry.Password);
            await users.InsertAsync(user);
            output.WriteLine($"Added    {email} as {user.Role}");
        }

        return 0;
    }

    private static IEnumerable<string> Validate(AccountEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.Name)) yield return "name is missing.";
        if (string.IsNullOrWhiteSpace(entry.Email) || !entry.Email.Contains('@')) yield return "email is missing or not valid.";
        if (!Roles.Any(r => r.Equals(entry.Role?.Trim(), StringComparison.OrdinalIgnoreCase)))
            yield return "role must be Requester, Staff or Supervisor.";
        if (string.IsNullOrEmpty(entry.Password) || entry.Password.Length < 8) yield return "password must be at least 8 characters.";
    }

    private sealed class AccountEntry
    {
        public string Name { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
    }
}
