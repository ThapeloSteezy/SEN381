using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests.BlackBox;

// Black-box technique: decision table. Conditions are who is acting and the current status;
// the action is assign / start / fix / close; the outcome is allowed or refused.
public sealed class RoleActionDecisionTableTests
{
    private static readonly UserRef Requester = new() { Id = "u1", Email = "naledi@example.org", Name = "Naledi" };
    private static readonly UserRef Supervisor = new() { Id = "sup", Email = "pieter@example.org", Name = "Pieter" };
    private static readonly UserRef Assignee = new() { Id = "s1", Email = "thandi@example.org", Name = "Thandi" };
    private static readonly UserRef OtherStaff = new() { Id = "s2", Email = "johan@example.org", Name = "Johan" };

    public static TheoryData<string, RequestStatus, string, bool> Cases() => new()
    {
        // actor, status, action, expected success
        { "supervisor", RequestStatus.New, "assign", true },
        { "supervisor", RequestStatus.Assigned, "reassign", true },
        { "supervisor", RequestStatus.Closed, "assign", false },
        { "supervisor", RequestStatus.New, "assign-requester", false },
        { "supervisor", RequestStatus.Resolved, "close", true },
        { "supervisor", RequestStatus.Assigned, "start", true },
        { "assignee", RequestStatus.Assigned, "start", true },
        { "assignee", RequestStatus.InProgress, "fix", true },
        { "assignee", RequestStatus.Resolved, "close", false },
        { "assignee", RequestStatus.Assigned, "close", false },
        { "assignee", RequestStatus.New, "start", false },
        { "other-staff", RequestStatus.Assigned, "start", false },
        { "other-staff", RequestStatus.InProgress, "fix", false },
        { "other-staff", RequestStatus.Resolved, "close", false }
    };

    [Theory]
    [MemberData(nameof(Cases))]
    [Trait("Technique", "DecisionTable")]
    [Trait("Requirement", "FR-07")]
    [Trait("Requirement", "FR-08")]
    public async Task Role_and_status_decide_whether_the_action_is_allowed(
        string actor, RequestStatus status, string action, bool expectedSuccess)
    {
        var (service, request) = await SeedAsync(status);
        var (user, isSupervisor) = Actor(actor);

        var result = action switch
        {
            "assign" => await service.AssignAsync(request.Id, status, "s1", user, null),
            "reassign" => await service.AssignAsync(request.Id, status, "s2", user, null),
            "assign-requester" => await service.AssignAsync(request.Id, status, "u1", user, null),
            "start" => await service.ChangeStatusAsync(request.Id, status, RequestStatus.InProgress, user, isSupervisor, null),
            "fix" => await service.ChangeStatusAsync(request.Id, status, RequestStatus.Resolved, user, isSupervisor, null),
            "close" => await service.ChangeStatusAsync(request.Id, status, RequestStatus.Closed, user, isSupervisor, null),
            _ => throw new ArgumentOutOfRangeException(nameof(action), action, null)
        };

        Assert.Equal(expectedSuccess, result.Success);
        if (!expectedSuccess)
            Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    private static (UserRef User, bool IsSupervisor) Actor(string actor) => actor switch
    {
        "supervisor" => (Supervisor, true),
        "assignee" => (Assignee, false),
        "other-staff" => (OtherStaff, false),
        _ => throw new ArgumentOutOfRangeException(nameof(actor), actor, null)
    };

    private static async Task<(RequestService Service, ServiceRequest Request)> SeedAsync(RequestStatus status)
    {
        var store = new InMemoryStore();
        var users = new InMemoryUsers(
            new ApplicationUser { Id = "s1", Email = "thandi@example.org", DisplayName = "Thandi", Role = UserRoles.Staff },
            new ApplicationUser { Id = "s2", Email = "johan@example.org", DisplayName = "Johan", Role = UserRoles.Staff },
            new ApplicationUser { Id = "u1", Email = "naledi@example.org", DisplayName = "Naledi", Role = UserRoles.Requester });
        var service = new RequestService(store, users, new StatusTransitionPolicy());

        var request = await service.CreateAsync(Requester, "Leaking tap", "Kitchen tap drips all day.", 5, "kitchen", RequestPriority.Medium);
        if (status == RequestStatus.New)
            return (service, request);

        Assert.True((await service.AssignAsync(request.Id, RequestStatus.New, "s1", Supervisor, null)).Success);
        if (status == RequestStatus.Assigned)
            return (service, request);

        Assert.True((await service.ChangeStatusAsync(request.Id, RequestStatus.Assigned, RequestStatus.InProgress, Assignee, false, null)).Success);
        if (status == RequestStatus.InProgress)
            return (service, request);

        Assert.True((await service.ChangeStatusAsync(request.Id, RequestStatus.InProgress, RequestStatus.Resolved, Assignee, false, null)).Success);
        if (status == RequestStatus.Resolved)
            return (service, request);

        Assert.True((await service.ChangeStatusAsync(request.Id, RequestStatus.Resolved, RequestStatus.Closed, Supervisor, true, null)).Success);
        return (service, request);
    }
}
