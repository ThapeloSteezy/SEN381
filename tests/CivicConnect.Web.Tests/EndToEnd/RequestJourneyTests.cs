using CivicConnect.Web.Tests.Integration;
using Microsoft.Playwright;

namespace CivicConnect.Web.Tests.EndToEnd;

[Collection(MongoTests.Name)]
[Trait("Category", "E2E")]
[Trait("Requirement", "FR-08")]
public sealed class RequestJourneyTests : IAsyncLifetime
{
    private readonly MongoServer _server;
    private CivicConnectApp _app = null!;
    private IPlaywright _playwright = null!;
    private IBrowser _browser = null!;

    public RequestJourneyTests(MongoServer server) => _server = server;

    public async Task InitializeAsync()
    {
        _app = await CivicConnectApp.StartAsync(_server, kestrel: true);
        Assert.Matches(@"https?://127\.0\.0\.1:\d+", _app.PublicUrl);
        using (var probe = new HttpClient())
        {
            var login = await probe.GetAsync(_app.PublicUrl + "/Account/Login");
            Assert.True(login.IsSuccessStatusCode, $"Login page at {_app.PublicUrl} returned {(int)login.StatusCode}: {await login.Content.ReadAsStringAsync()}");
            Assert.Contains("Email", await login.Content.ReadAsStringAsync());
        }
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new() { Headless = true });
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
            await _browser.DisposeAsync();
        _playwright?.Dispose();
        _app.Dispose();
    }

    [Fact]
    public async Task Requester_reports_supervisor_assigns_staff_fixes_supervisor_closes()
    {
        await using var requester = await OpenAsAsync(CivicConnectApp.Requester);
        await requester.GotoAsync(_app.PublicUrl + "/Requests/Create");
        await requester.GetByText("Maintenance", new() { Exact = true }).ClickAsync();
        await requester.GetByText("Kitchen", new() { Exact = true }).ClickAsync();
        await requester.GetByLabel("What happened?").FillAsync("The kitchen tap has been leaking since Monday.");
        await requester.GetByText("Fix today", new() { Exact = true }).ClickAsync();
        await requester.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();
        await requester.GetByText("Thank you. We got your request.").WaitForAsync();
        var detailsUrl = requester.Url;

        await using var supervisor = await OpenAsAsync(CivicConnectApp.Supervisor);
        await supervisor.GotoAsync(detailsUrl);
        await supervisor.GetByLabel("Who should fix this?").SelectOptionAsync(new SelectOptionValue { Label = "Sipho Staff" });
        await supervisor.GetByRole(AriaRole.Button, new() { Name = "Assign" }).ClickAsync();
        await supervisor.GetByText("Assigned.").WaitForAsync();

        await using var staff = await OpenAsAsync(CivicConnectApp.Staff);
        await staff.GotoAsync(detailsUrl);
        await staff.GetByRole(AriaRole.Button, new() { Name = "Start work" }).ClickAsync();
        await staff.GetByText("Status changed to In Progress.").WaitForAsync();
        await staff.GetByRole(AriaRole.Button, new() { Name = "Mark as fixed" }).ClickAsync();
        await staff.GetByText("Status changed to Resolved.").WaitForAsync();

        await supervisor.GotoAsync(detailsUrl);
        await supervisor.GetByRole(AriaRole.Button, new() { Name = "Close request" }).ClickAsync();
        await supervisor.GetByText("Status changed to Closed.").WaitForAsync();
        await Assertions.Expect(supervisor.Locator(".status-box strong")).ToHaveTextAsync("All done");
    }

    [Fact]
    public async Task Staff_member_is_not_offered_close_on_a_fixed_request()
    {
        await using var requester = await OpenAsAsync(CivicConnectApp.Requester);
        await requester.GotoAsync(_app.PublicUrl + "/Requests/Create");
        await requester.GetByText("IT Support", new() { Exact = true }).ClickAsync();
        await requester.GetByText("Server room", new() { Exact = true }).ClickAsync();
        await requester.GetByLabel("What happened?").FillAsync("The access point in the server room keeps dropping.");
        await requester.GetByText("Fix this week", new() { Exact = true }).ClickAsync();
        await requester.GetByRole(AriaRole.Button, new() { Name = "Send" }).ClickAsync();
        await requester.GetByText("Thank you. We got your request.").WaitForAsync();
        var detailsUrl = requester.Url;

        await using var supervisor = await OpenAsAsync(CivicConnectApp.Supervisor);
        await supervisor.GotoAsync(detailsUrl);
        await supervisor.GetByLabel("Who should fix this?").SelectOptionAsync(new SelectOptionValue { Label = "Sipho Staff" });
        await supervisor.GetByRole(AriaRole.Button, new() { Name = "Assign" }).ClickAsync();
        await supervisor.GetByText("Assigned.").WaitForAsync();

        await using var staff = await OpenAsAsync(CivicConnectApp.Staff);
        await staff.GotoAsync(detailsUrl);
        await staff.GetByRole(AriaRole.Button, new() { Name = "Start work" }).ClickAsync();
        await staff.GetByRole(AriaRole.Button, new() { Name = "Mark as fixed" }).ClickAsync();
        await staff.GetByText("Status changed to Resolved.").WaitForAsync();

        Assert.Equal(0, await staff.GetByRole(AriaRole.Button, new() { Name = "Close request" }).CountAsync());
        await supervisor.GotoAsync(detailsUrl);
        Assert.Equal(1, await supervisor.GetByRole(AriaRole.Button, new() { Name = "Close request" }).CountAsync());
    }

    private async Task<IPage> OpenAsAsync(CivicConnect.Web.Domain.Entities.ApplicationUser user)
    {
        var context = await _browser.NewContextAsync();
        var page = await context.NewPageAsync();
        await page.GotoAsync(_app.PublicUrl + "/Account/Login");
        await page.GetByLabel("Email").FillAsync(user.Email);
        await page.GetByLabel("Password").FillAsync(CivicConnectApp.Password);
        await page.GetByRole(AriaRole.Button, new() { Name = "Sign in" }).ClickAsync();
        await page.WaitForURLAsync(url => !url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase));
        return page;
    }
}
