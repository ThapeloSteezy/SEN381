using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Domain.Entities;

public class RequestHistory
{
    public string ActionType { get; set; } = string.Empty;
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus? ToStatus { get; set; }
    public string? Notes { get; set; }
    public UserRef? PerformedBy { get; set; }
    public UserRef? AssignedTo { get; set; }
    public DateTime PerformedAtUtc { get; set; }
}

public class UserRef
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Name { get; set; }

    [MongoDB.Bson.Serialization.Attributes.BsonIgnore]
    public string Label => string.IsNullOrWhiteSpace(Name) ? Email : Name;
}
