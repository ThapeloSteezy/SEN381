using CivicConnect.Web.Application;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests.Integration;

[Trait("Requirement", "NFR-06")]
public sealed class MongoRequestStoreTests(MongoServer server) : IntegrationTest(server)
{
    private MongoRequestStore Store => new(App.Mongo);

    [Fact]
    public async Task Update_based_on_an_old_version_is_refused_and_the_newer_change_is_kept()
    {
        var request = NewRequest("CC-TEST-0001");
        await Store.InsertAsync(request);

        var first = await App.LoadAsync(request.Id);
        var second = await App.LoadAsync(request.Id);

        first.Status = RequestStatus.Assigned;
        Assert.True(await Store.TryUpdateAsync(first, previousVersion: 1));

        second.Status = RequestStatus.Closed;
        Assert.False(await Store.TryUpdateAsync(second, previousVersion: 1));

        var stored = await App.LoadAsync(request.Id);
        Assert.Equal(RequestStatus.Assigned, stored.Status);
        Assert.Equal(2, stored.Version);
    }

    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task Two_requests_cannot_share_a_request_number()
    {
        await Store.InsertAsync(NewRequest("CC-TEST-0002"));

        await Assert.ThrowsAsync<DuplicateRequestNumberException>(() => Store.InsertAsync(NewRequest("CC-TEST-0002")));
    }

    [Fact]
    [Trait("Requirement", "FR-13")]
    public async Task Non_supervisors_only_get_requests_they_reported_or_are_assigned()
    {
        await Store.InsertAsync(NewRequest("CC-TEST-0003", submittedBy: "req1"));
        await Store.InsertAsync(NewRequest("CC-TEST-0004", submittedBy: "req2", assignedTo: "staff1"));
        await Store.InsertAsync(NewRequest("CC-TEST-0005", submittedBy: "req2"));

        var requester = await Store.GetVisibleToUserAsync("req1", staffAccess: false);
        var staff = await Store.GetVisibleToUserAsync("staff1", staffAccess: false);
        var supervisor = await Store.GetVisibleToUserAsync("sup", staffAccess: true);

        Assert.Equal(["CC-TEST-0003"], requester.Select(request => request.RequestNumber));
        Assert.Equal(["CC-TEST-0004"], staff.Select(request => request.RequestNumber));
        Assert.Equal(3, supervisor.Count);
    }

    private static ServiceRequest NewRequest(string number, string submittedBy = "req1", string? assignedTo = null) => new()
    {
        Id = Guid.NewGuid(),
        RequestNumber = number,
        Title = "Broken window",
        Description = "The window in the hall is cracked.",
        CategoryId = 1,
        RoomId = "hall",
        SubmittedById = submittedBy,
        AssignedToId = assignedTo,
        Status = assignedTo is null ? RequestStatus.New : RequestStatus.Assigned,
        CreatedAtUtc = DateTime.UtcNow,
        UpdatedAtUtc = DateTime.UtcNow
    };
}
