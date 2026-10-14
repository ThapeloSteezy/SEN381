using CivicConnect.Web;
using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using MongoDB.Bson;
using MongoDB.Driver;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MongoContext>();
builder.Services.AddSingleton<DatabaseStatus>();
builder.Services.AddScoped<IRequestStore, MongoRequestStore>();
builder.Services.AddScoped<MongoUserDirectory>();
builder.Services.AddScoped<IUserDirectory>(services => services.GetRequiredService<MongoUserDirectory>());
builder.Services.AddScoped<IStatusTransitionPolicy, StatusTransitionPolicy>();
builder.Services.AddScoped<IRequestService, RequestService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
    });
builder.Services.AddAuthorization();

builder.Services.AddControllersWithViews();

// Render (and any other HTTPS proxy) talks to the app over HTTP and sends X-Forwarded-Proto.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownIPNetworks.Clear();
    options.KnownProxies.Clear();
});

var app = builder.Build();
AppInfo.EnvironmentName = app.Environment.EnvironmentName;

app.UseForwardedHeaders();

if (app.Environment.IsProduction())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.Use(DatabaseGuard.InvokeAsync);
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", async (MongoContext mongo, DatabaseStatus database, CancellationToken cancellationToken) =>
{
    var body = new Dictionary<string, string?>
    {
        ["status"] = database.Ready ? "ok" : "unavailable",
        ["version"] = AppInfo.Version,
        ["environment"] = AppInfo.EnvironmentName
    };

    if (!database.Ready)
    {
        body["error"] = database.Error;
        return Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    try
    {
        await mongo.Database.RunCommandAsync<BsonDocument>(new BsonDocument("ping", 1), cancellationToken: cancellationToken);
        return Results.Json(body);
    }
    catch (MongoException)
    {
        body["status"] = "unavailable";
        body["error"] = "The database did not respond.";
        return Results.Json(body, statusCode: StatusCodes.Status503ServiceUnavailable);
    }
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    if (args is ["import-accounts", var accountsFile, ..])
        return await AccountImport.RunAsync(scope.ServiceProvider, accountsFile, Console.Out);

    var database = scope.ServiceProvider.GetRequiredService<DatabaseStatus>();
    try
    {
        await DatabaseSetup.RunAsync(scope.ServiceProvider);
        database.Ready = true;
    }
    catch (Exception ex)
    {
        database.Ready = false;
        database.Error = "CivicConnect could not reach the database. Check the connection string and that MongoDB is available.";
        Console.Error.WriteLine($"Database setup failed: {ex}");
    }
}

app.Run();
return 0;
