using CivicConnect.Web.Domain.Enums;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace CivicConnect.Web.Domain.Entities;

public class ServiceRequest
{
    [BsonId]
    [BsonGuidRepresentation(GuidRepresentation.Standard)]
    public Guid Id { get; set; }

    public string RequestNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public RequestCategory? Category { get; set; }

    public string? RoomId { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.New;
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    public string SubmittedById { get; set; } = string.Empty;
    public string? AssignedToId { get; set; }
    public UserRef? AssignedTo { get; set; }

    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<RequestHistory> History { get; set; } = [];
    public List<RequestFeedback> Feedback { get; set; } = [];
}
