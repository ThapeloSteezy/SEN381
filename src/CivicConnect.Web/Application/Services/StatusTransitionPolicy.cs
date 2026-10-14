using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Services;

public sealed class StatusTransitionPolicy : IStatusTransitionPolicy
{
    public bool IsAllowed(RequestStatus current, RequestStatus requested)
        => current == requested || current.Next() == requested;
}
