using System.Net;
using CivicConnect.Web.Domain.Enums;
using MongoDB.Driver;

namespace CivicConnect.Web.Tests.Integration;

public sealed class RequestWorkflowTests(MongoServer server) : IntegrationTest(server)
{
    [Fact]
    [Trait("Requirement", "FR-08")]
    [Trait("Requirement", "FR-12")]
    public async Task Report_assign_fix_and_close_is_stored_with_full_history()
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);
        var staff = await App.SignInAsync(CivicConnectApp.Staff);

        var id = await ReportAsync(requester);
        await AssignAsync(supervisor, id, RequestStatus.New, CivicConnectApp.Staff.Id);
        await ChangeStatusAsync(staff, id, RequestStatus.Assigned, RequestStatus.InProgress);
        await ChangeStatusAsync(staff, id, RequestStatus.InProgress, RequestStatus.Resolved);
        await ChangeStatusAsync(supervisor, id, RequestStatus.Resolved, RequestStatus.Closed);

        var stored = await App.LoadAsync(id);
        Assert.Equal(RequestStatus.Closed, stored.Status);
        Assert.Equal("kitchen", stored.RoomId);
        Assert.Equal(CivicConnectApp.Staff.Id, stored.AssignedToId);
        Assert.Equal(
            ["Submitted", "Assigned", "StatusChanged", "StatusChanged", "StatusChanged"],
            stored.History.Select(entry => entry.ActionType));
        Assert.Equal(
            [CivicConnectApp.Requester.Id, CivicConnectApp.Supervisor.Id, CivicConnectApp.Staff.Id, CivicConnectApp.Staff.Id, CivicConnectApp.Supervisor.Id],
            stored.History.Select(entry => entry.PerformedBy!.Id));
    }

    [Fact]
    [Trait("Requirement", "FR-08")]
    public async Task Staff_who_is_not_assigned_cannot_change_the_job()
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);
        var id = await ReportAsync(requester);
        await AssignAsync(supervisor, id, RequestStatus.New, CivicConnectApp.Staff.Id);
        var before = await App.LoadAsync(id);

        var otherStaff = await App.SignInAsync(CivicConnectApp.OtherStaff);
        await ChangeStatusAsync(otherStaff, id, RequestStatus.Assigned, RequestStatus.InProgress);

        var after = await App.LoadAsync(id);
        Assert.Equal(RequestStatus.Assigned, after.Status);
        Assert.Equal(before.Version, after.Version);
    }

    [Fact]
    [Trait("Requirement", "FR-07")]
    public async Task Assigned_staff_member_sees_a_new_update_until_they_open_the_request()
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);
        var staff = await App.SignInAsync(CivicConnectApp.Staff);
        var id = await ReportAsync(requester);
        await AssignAsync(supervisor, id, RequestStatus.New, CivicConnectApp.Staff.Id);
        var versionAfterAssign = (await App.LoadAsync(id)).Version;

        Assert.Contains("Updates, 1 new", await staff.Client.GetStringAsync("/"));

        Assert.Equal(HttpStatusCode.OK, (await staff.GetAsync($"/Requests/Details/{id}")).StatusCode);

        Assert.DoesNotContain("Updates, 1 new", await staff.Client.GetStringAsync("/"));
        var stored = await App.LoadAsync(id);
        Assert.All(stored.Feedback.Where(item => item.RecipientUserId == CivicConnectApp.Staff.Id), item => Assert.True(item.IsRead));
        Assert.Contains(stored.Feedback, item => item.RecipientUserId == CivicConnectApp.Requester.Id && !item.IsRead);
        Assert.Equal(versionAfterAssign, stored.Version);
    }

    [Fact]
    public async Task Report_for_a_room_that_is_not_on_the_plan_is_rejected_and_not_saved()
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);

        var response = await requester.PostFormAsync("/Requests/Create", "/Requests/Create", new()
        {
            ["Description"] = "The light in the basement is broken.",
            ["CategoryId"] = "1",
            ["RoomId"] = "basement",
            ["Priority"] = nameof(RequestPriority.Medium)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(0, await App.Mongo.Requests.CountDocumentsAsync(FilterDefinition<Domain.Entities.ServiceRequest>.Empty));
    }
}
