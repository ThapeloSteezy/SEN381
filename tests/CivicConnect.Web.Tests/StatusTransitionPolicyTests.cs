using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests;

public class StatusTransitionPolicyTests
{
    private readonly StatusTransitionPolicy _policy = new();

    [Fact]
    public void New_to_Assigned_is_allowed()
        => Assert.True(_policy.IsAllowed(RequestStatus.New, RequestStatus.Assigned));

    [Fact]
    public void Assigned_to_InProgress_is_allowed()
        => Assert.True(_policy.IsAllowed(RequestStatus.Assigned, RequestStatus.InProgress));

    [Fact]
    public void New_to_Closed_is_rejected()
        => Assert.False(_policy.IsAllowed(RequestStatus.New, RequestStatus.Closed));

    [Fact]
    public void Closed_to_InProgress_is_rejected()
        => Assert.False(_policy.IsAllowed(RequestStatus.Closed, RequestStatus.InProgress));

    [Fact]
    public void Same_status_is_idempotently_allowed()
        => Assert.True(_policy.IsAllowed(RequestStatus.InProgress, RequestStatus.InProgress));
}
