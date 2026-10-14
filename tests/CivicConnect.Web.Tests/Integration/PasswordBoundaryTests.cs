using System.Net;
using CivicConnect.Web.Data;
using Microsoft.Extensions.DependencyInjection;

namespace CivicConnect.Web.Tests.Integration;

// Black-box technique: boundary value analysis on the 8-character password rule.
[Trait("Technique", "BVA")]
[Trait("Requirement", "FR-11")]
public sealed class PasswordBoundaryTests(MongoServer server) : IntegrationTest(server)
{
    [Theory]
    [InlineData("seven77", false)]
    [InlineData("eight888", true)]
    public async Task Adding_a_person_rejects_7_characters_and_accepts_8(string password, bool accepted)
    {
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);
        var email = $"bva-{password}@test.local";

        var response = await supervisor.PostFormAsync("/People", "/People/Add", new()
        {
            ["NewPerson.Name"] = "Boundary Person",
            ["NewPerson.Email"] = email,
            ["NewPerson.Role"] = UserRoles.Requester,
            ["NewPerson.Password"] = password
        });

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.NotNull(await App.Services.GetUser(email));
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Use at least 8 characters.", await response.Content.ReadAsStringAsync());
            Assert.Null(await App.Services.GetUser(email));
        }
    }

    [Theory]
    [InlineData("seven77", false)]
    [InlineData("eight888", true)]
    public async Task Changing_your_password_rejects_7_characters_and_accepts_8(string password, bool accepted)
    {
        var requester = await App.SignInAsync(CivicConnectApp.Requester);

        var response = await requester.PostFormAsync("/Account/Password", "/Account/Password", new()
        {
            ["CurrentPassword"] = CivicConnectApp.Password,
            ["NewPassword"] = password,
            ["ConfirmPassword"] = password
        });

        if (accepted)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            var signIn = await App.NewBrowser().SignInAsync(CivicConnectApp.Requester.Email, password);
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        }
        else
        {
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Contains("Use at least 8 characters.", await response.Content.ReadAsStringAsync());
        }
    }

    [Theory]
    [InlineData("seven77", false)]
    [InlineData("eight888", true)]
    public async Task Resetting_someone_elses_password_rejects_7_characters_and_accepts_8(string password, bool accepted)
    {
        var supervisor = await App.SignInAsync(CivicConnectApp.Supervisor);

        var response = await supervisor.PostFormAsync("/People", "/People/ResetPassword", new()
        {
            ["id"] = CivicConnectApp.OtherRequester.Id,
            ["password"] = password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var page = await (await supervisor.GetAsync("/People")).Content.ReadAsStringAsync();
        if (accepted)
            Assert.Contains("New password set", page);
        else
            Assert.Contains("Use at least 8 characters", page);
    }
}

file static class UserLookup
{
    public static async Task<CivicConnect.Web.Domain.Entities.ApplicationUser?> GetUser(this IServiceProvider services, string email)
    {
        using var scope = services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<CivicConnect.Web.Data.MongoUserDirectory>()
            .FindByEmailAsync(email);
    }
}
