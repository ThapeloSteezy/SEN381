using MongoDB.Driver;

namespace CivicConnect.Web.Data;

public static class DatabaseGuard
{
    public static async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/health"))
        {
            await next(context);
            return;
        }

        var status = context.RequestServices.GetRequiredService<DatabaseStatus>();
        if (!status.Ready)
        {
            await WriteUnavailableAsync(context, status.Error ?? "The database is not available.");
            return;
        }

        try
        {
            await next(context);
        }
        catch (Exception ex) when (ex is MongoException or TimeoutException)
        {
            await WriteUnavailableAsync(context, "The database could not be reached. Please try again shortly.");
        }
    }

    private static async Task WriteUnavailableAsync(HttpContext context, string message)
    {
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        context.Response.ContentType = "text/html; charset=utf-8";
        var safe = System.Net.WebUtility.HtmlEncode(message);
        await context.Response.WriteAsync(
            "<!DOCTYPE html><html lang=\"en\"><head><meta charset=\"utf-8\" />"
            + "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\" />"
            + "<title>Database unavailable - CivicConnect</title>"
            + "<style>body{font-family:system-ui,sans-serif;margin:3rem auto;max-width:36rem;color:#1f2937}h1{font-size:1.5rem}p{color:#5f6b7a}</style>"
            + "</head><body><h1>CivicConnect cannot reach the database</h1>"
            + "<p>" + safe + "</p>"
            + "<p>Check the connection string and that MongoDB is running, then try again.</p>"
            + "</body></html>");
    }
}
