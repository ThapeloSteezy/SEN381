using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests;

public sealed class RequestNumberRetryTests
{
    private static readonly UserRef Requester = new() { Id = "u1", Email = "naledi@example.org", Name = "Naledi" };

    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task Create_retries_when_the_request_number_is_already_used()
    {
        var store = new InMemoryStore { FailInserts = 2 };
        var service = new RequestService(store, Users(), new StatusTransitionPolicy());

        var request = await service.CreateAsync(Requester, "Leaking tap", "Kitchen tap drips all day.", 5, "kitchen", RequestPriority.Medium);

        Assert.Equal(3, store.InsertAttempts);
        Assert.Contains(request.Id, store.Requests.Select(item => item.Id));
        Assert.StartsWith("CC-", request.RequestNumber);
    }

    [Fact]
    [Trait("Requirement", "FR-14")]
    public async Task Create_gives_up_after_repeated_collisions()
    {
        var store = new InMemoryStore { FailInserts = 8 };
        var service = new RequestService(store, Users(), new StatusTransitionPolicy());

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.CreateAsync(Requester, "Leaking tap", "Kitchen tap drips all day.", 5, "kitchen", RequestPriority.Medium));

        Assert.Equal("Could not save the request. Please try again.", error.Message);
        Assert.Empty(store.Requests);
        Assert.Equal(8, store.InsertAttempts);
    }

    private static InMemoryUsers Users() => new(
        new ApplicationUser { Id = "s1", Email = "thandi@example.org", DisplayName = "Thandi", Role = UserRoles.Staff });
}
