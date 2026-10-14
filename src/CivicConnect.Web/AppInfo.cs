using System.Reflection;

namespace CivicConnect.Web;

public static class AppInfo
{
    public static string Version { get; } = ReadVersion();
    public static string EnvironmentName { get; set; } = "Development";

    private static string ReadVersion()
    {
        var fromEnv = Environment.GetEnvironmentVariable("APP_VERSION");
        if (!string.IsNullOrWhiteSpace(fromEnv))
            return fromEnv.Trim();

        var sha = Environment.GetEnvironmentVariable("GITHUB_SHA");
        if (!string.IsNullOrWhiteSpace(sha))
            return sha.Length >= 7 ? sha[..7] : sha;

        var informational = typeof(Program).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (string.IsNullOrWhiteSpace(informational))
            return "dev";

        var plus = informational.IndexOf('+');
        return plus < 0 ? informational : informational[..plus];
    }
}
