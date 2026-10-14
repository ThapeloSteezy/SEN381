using MongoDB.Bson.Serialization.Attributes;

namespace CivicConnect.Web.Domain.Entities;

public class ApplicationUser
{
    [BsonId]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Email { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
