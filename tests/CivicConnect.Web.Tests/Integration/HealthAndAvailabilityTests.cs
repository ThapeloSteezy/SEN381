using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CivicConnect.Web;

namespace CivicConnect.Web.Tests.Integration;

public sealed class HealthAndAvailabilityTests(MongoServer server) : IntegrationTest(server)
{
    [Fact]
    public async Task Health_reports_ok_and_the_running_version_when_MongoDB_is_up()
    {
        var response = await App.NewBrowser().GetAsync("/health");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("Testing", body.GetProperty("environment").GetString());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("version").GetString()));
        Assert.Contains(AppInfo.Version, await (await App.NewBrowser().GetAsync("/Account/Login")).Content.ReadAsStringAsync());
    }
}

[Trait("Category", "Integration")]
public sealed class UnreachableDatabaseTests
{
    [Fact]
    public async Task Unreachable_database_does_not_crash_the_app()
    {
        using var app = CivicConnectApp.Unreachable();
        var browser = app.NewBrowser();

        var home = await browser.GetAsync("/");
        var homeBody = await home.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, home.StatusCode);
        Assert.Contains("cannot reach the database", homeBody, StringComparison.OrdinalIgnoreCase);

        var health = await browser.GetAsync("/health");
        var healthBody = await health.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.ServiceUnavailable, health.StatusCode);
        Assert.Equal("unavailable", healthBody.GetProperty("status").GetString());
    }
}
