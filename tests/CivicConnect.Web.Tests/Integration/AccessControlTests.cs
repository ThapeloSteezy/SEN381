using System.Net;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Tests.Integration;

[Trait("Requirement", "FR-11")]
public sealed class AccessControlTests(MongoServer server) : IntegrationTest(server)
{
    [Fact]
    public async Task Wrong_password_does_not_sign_in()
    {
        var browser = App.NewBrowser();

        var response = await browser.SignInAsync(CivicConnectApp.Requester.Email, "not-the-password");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Incorrect email or password.", await response.Content.ReadAsStringAsync());
        var after = await browser.GetAsync("/Requests");
        Assert.Equal(HttpStatusCode.Redirect, after.StatusCode);
        Assert.StartsWith("/Account/Login", after.Headers.Location!.PathAndQuery);
    }

    [Fact]
    public async Task Signed_out_visitor_is_sent_to_sign_in()
    {
        var response = await App.NewBrowser().GetAsync("/Requests/Updates");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/Login", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    [Trait("Requirement", "FR-13")]
    public async Task Only_the_owner_the_assignee_and_supervisors_can_open_a_request()
    {
        var owner = await App.SignInAsync(CivicConnectApp.Requester);
        var id = await ReportAsync(owner);
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);
        await AssignAsync(supervisor, id, RequestStatus.New, CivicConnectApp.Staff.Id);

        var page = $"/Requests/Details/{id}";
        Assert.Equal(HttpStatusCode.OK, (await owner.GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await supervisor.GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await (await App.SignInAsync(CivicConnectApp.Staff)).GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await App.SignInAsync(CivicConnectApp.OtherRequester)).GetAsync(page)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await (await App.SignInAsync(CivicConnectApp.OtherStaff)).GetAsync(page)).StatusCode);
    }

    [Theory]
    [InlineData("/Requests/Map")]
    [InlineData("/People")]
    public async Task Requester_cannot_open_supervisor_pages(string page)
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);

        var response = await requester.GetAsync(page);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
    }

    [Fact]
    [Trait("Requirement", "FR-07")]
    public async Task Requester_posting_the_assign_form_is_refused_and_nothing_changes()
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);
        var id = await ReportAsync(requester);

        var response = await AssignAsync(requester, id, RequestStatus.New, CivicConnectApp.Staff.Id);

        Assert.StartsWith("/Account/AccessDenied", response.Headers.Location!.PathAndQuery);
        var stored = await App.LoadAsync(id);
        Assert.Equal(RequestStatus.New, stored.Status);
        Assert.Null(stored.AssignedToId);
    }
}
