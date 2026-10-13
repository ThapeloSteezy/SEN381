using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Application.Services;
using CivicConnect.Web.Data;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<MongoContext>();
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

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseStatusCodePagesWithReExecute("/Home/Error", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

using (var scope = app.Services.CreateScope())
{
    if (args is ["import-accounts", var accountsFile, ..])
        return await AccountImport.RunAsync(scope.ServiceProvider, accountsFile, Console.Out);

    await DatabaseSetup.RunAsync(scope.ServiceProvider);
}

app.Run();
return 0;
