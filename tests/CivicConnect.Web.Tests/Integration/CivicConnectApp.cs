using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MongoDB.Driver;

namespace CivicConnect.Web.Tests.Integration;

// The real app (controllers, views, auth, MongoDB stores) running in memory against a fresh database.
public sealed class CivicConnectApp : WebApplicationFactory<Program>
{
    public const string Password = "Test-password-1";

    private readonly string _connectionString;
    private readonly bool _kestrel;
    private readonly bool _dropDatabase;
    private IHost? _kestrelHost;

    private CivicConnectApp(string connectionString, bool kestrel = false, bool dropDatabase = true)
    {
        _connectionString = connectionString;
        _kestrel = kestrel;
        _dropDatabase = dropDatabase;
    }

    public static readonly ApplicationUser Supervisor = User("sup", "supervisor@test.local", "Sam Supervisor", UserRoles.Supervisor);
    public static readonly ApplicationUser Staff = User("staff1", "staff1@test.local", "Sipho Staff", UserRoles.Staff);
    public static readonly ApplicationUser OtherStaff = User("staff2", "staff2@test.local", "Thandi Staff", UserRoles.Staff);
    public static readonly ApplicationUser Requester = User("req1", "requester1@test.local", "Naledi Requester", UserRoles.Requester);
    public static readonly ApplicationUser OtherRequester = User("req2", "requester2@test.local", "Kagiso Requester", UserRoles.Requester);

    public string PublicUrl { get; private set; } = "";

    public static CivicConnectApp Unreachable()
        => new("mongodb://127.0.0.1:1/civicconnect_unreachable?serverSelectionTimeoutMS=2000", dropDatabase: false);

    public static async Task<CivicConnectApp> StartAsync(MongoServer server, bool kestrel = false)
    {
        var app = new CivicConnectApp(server.ConnectionStringFor("cc_test_" + Guid.NewGuid().ToString("N")[..12]), kestrel);
        using var scope = app.Services.CreateScope();
        await DatabaseSetup.RunAsync(scope.ServiceProvider);

        var users = scope.ServiceProvider.GetRequiredService<MongoUserDirectory>();
        var hasher = new PasswordHasher<ApplicationUser>();
        foreach (var user in new[] { Supervisor, Staff, OtherStaff, Requester, OtherRequester })
        {
            var copy = new ApplicationUser { Id = user.Id, Email = user.Email, DisplayName = user.DisplayName, Role = user.Role };
            copy.PasswordHash = hasher.HashPassword(copy, Password);
            await users.InsertAsync(copy);
        }

        return app;
    }

    public MongoContext Mongo => Services.GetRequiredService<MongoContext>();

    public Task<ServiceRequest> LoadAsync(Guid id)
        => Mongo.Requests.Find(request => request.Id == id).SingleAsync();

    public async Task<Browser> SignInAsync(ApplicationUser user)
    {
        var browser = NewBrowser();
        var response = await browser.SignInAsync(user.Email, Password);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return browser;
    }

    public Browser NewBrowser()
        => new(CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false, HandleCookies = true }));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:MongoDb", _connectionString);
        if (_kestrel)
            builder.UseUrls($"http://127.0.0.1:{FreePort()}");
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        if (!_kestrel)
            return base.CreateHost(builder);

        // WebApplicationFactory expects a TestServer host. Start Kestrel beside it so Playwright has a real TCP URL.
        var testHost = builder.Build();
        builder.ConfigureWebHost(webHost =>
        {
            webHost.UseKestrel();
            webHost.UseSetting("ConnectionStrings:MongoDb", _connectionString);
            webHost.UseEnvironment("Testing");
        });
        _kestrelHost = builder.Build();
        _kestrelHost.Start();
        var address = _kestrelHost.Services.GetRequiredService<IServer>()
            .Features.Get<IServerAddressesFeature>()!.Addresses.First();
        PublicUrl = address.TrimEnd('/');
        ClientOptions.BaseAddress = new Uri(PublicUrl + "/");
        testHost.Start();
        return testHost;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _kestrelHost?.Dispose();

        if (disposing && _dropDatabase)
        {
            var url = new MongoUrl(_connectionString);
            using var client = new MongoClient(url);
            client.DropDatabase(url.DatabaseName);
        }

        base.Dispose(disposing);
    }

    private static int FreePort()
    {
        using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        socket.Bind(new IPEndPoint(IPAddress.Loopback, 0));
        return ((IPEndPoint)socket.LocalEndPoint!).Port;
    }

    private static ApplicationUser User(string id, string email, string name, string role)
        => new() { Id = id, Email = email, DisplayName = name, Role = role };
}

// A signed-in (or anonymous) HTTP client that submits forms the way a browser does, antiforgery token included.
public sealed partial class Browser(HttpClient client)
{
    public HttpClient Client { get; } = client;

    public Task<HttpResponseMessage> GetAsync(string url) => Client.GetAsync(url);

    public async Task<HttpResponseMessage> SignInAsync(string email, string password)
        => await PostFormAsync("/Account/Login", "/Account/Login", new() { ["Email"] = email, ["Password"] = password });

    public async Task<HttpResponseMessage> PostFormAsync(string formPage, string action, Dictionary<string, string> fields)
    {
        var page = await Client.GetStringAsync(formPage);
        var token = AntiforgeryToken().Match(page);
        Assert.True(token.Success, $"No antiforgery token on {formPage}.");

        fields["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value);
        using var form = new FormUrlEncodedContent(fields);
        return await Client.PostAsync(action, form);
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryToken();
}
