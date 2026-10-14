using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Interfaces;

public interface IStatusTransitionPolicy
{
    bool IsAllowed(RequestStatus current, RequestStatus requested);
}
