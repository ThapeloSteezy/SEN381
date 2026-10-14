using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Domain.Entities;

public class RequestHistory
{
    public long Id { get; set; }
    public Guid ServiceRequestId { get; set; }
    public ServiceRequest? ServiceRequest { get; set; }

    public string ActionType { get; set; } = string.Empty;
    public RequestStatus? FromStatus { get; set; }
    public RequestStatus? ToStatus { get; set; }
    public string? Notes { get; set; }

    public string PerformedById { get; set; } = string.Empty;
    public ApplicationUser? PerformedBy { get; set; }
    public DateTime PerformedAtUtc { get; set; }
}
