using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Services;

public sealed class StatusTransitionPolicy : IStatusTransitionPolicy
{
    private static readonly IReadOnlyDictionary<RequestStatus, RequestStatus[]> Allowed =
        new Dictionary<RequestStatus, RequestStatus[]>
        {
            [RequestStatus.New] = [RequestStatus.Assigned],
            [RequestStatus.Assigned] = [RequestStatus.InProgress],
            [RequestStatus.InProgress] = [RequestStatus.Resolved],
            [RequestStatus.Resolved] = [RequestStatus.Closed],
            [RequestStatus.Closed] = []
        };

    public bool IsAllowed(RequestStatus from, RequestStatus to)
        => from == to || (Allowed.TryGetValue(from, out var next) && next.Contains(to));
}
