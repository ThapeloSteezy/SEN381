using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Domain.Entities;

public class ServiceRequest
{
    public Guid Id { get; set; }
    public string RequestNumber { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public int CategoryId { get; set; }
    public RequestCategory? Category { get; set; }

    public RequestStatus Status { get; set; } = RequestStatus.New;
    public RequestPriority Priority { get; set; } = RequestPriority.Medium;

    public string SubmittedById { get; set; } = string.Empty;
    public ApplicationUser? SubmittedBy { get; set; }

    public string? AssignedToId { get; set; }
    public ApplicationUser? AssignedTo { get; set; }

    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public ICollection<RequestHistory> History { get; set; } = new List<RequestHistory>();
    public ICollection<RequestFeedback> Feedback { get; set; } = new List<RequestFeedback>();
}
