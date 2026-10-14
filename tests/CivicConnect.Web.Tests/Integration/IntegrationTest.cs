using System.Net;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests.Integration;

[Collection(MongoTests.Name)]
[Trait("Category", "Integration")]
public abstract class IntegrationTest(MongoServer server) : IAsyncLifetime
{
    protected CivicConnectApp App { get; private set; } = null!;

    public async Task InitializeAsync() => App = await CivicConnectApp.StartAsync(server);

    public Task DisposeAsync()
    {
        App.Dispose();
        return Task.CompletedTask;
    }

    protected static async Task<Guid> ReportAsync(Browser requester, string roomId = "kitchen")
    {
        var response = await requester.PostFormAsync("/Requests/Create", "/Requests/Create", new()
        {
            ["Description"] = "The kitchen tap has been leaking since Monday.",
            ["CategoryId"] = "5",
            ["RoomId"] = roomId,
            ["Priority"] = nameof(RequestPriority.High)
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return Guid.Parse(response.Headers.Location!.OriginalString.Split('/').Last());
    }

    protected static Task<HttpResponseMessage> AssignAsync(Browser browser, Guid id, RequestStatus currentStatus, string staffId)
        => browser.PostFormAsync($"/Requests/Details/{id}", "/Requests/Assign", new()
        {
            ["id"] = id.ToString(),
            ["currentStatus"] = currentStatus.ToString(),
            ["staffId"] = staffId
        });

    protected static Task<HttpResponseMessage> ChangeStatusAsync(Browser browser, Guid id, RequestStatus currentStatus, RequestStatus newStatus)
        => browser.PostFormAsync("/", "/Requests/ChangeStatus", new()
        {
            ["id"] = id.ToString(),
            ["currentStatus"] = currentStatus.ToString(),
            ["newStatus"] = newStatus.ToString()
        });
}
