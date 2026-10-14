namespace CivicConnect.Web.Domain.Enums;

public static class RequestStatusText
{
    public static readonly RequestStatus[] Lifecycle =
        [RequestStatus.New, RequestStatus.Assigned, RequestStatus.InProgress, RequestStatus.Resolved, RequestStatus.Closed];

    public static string Label(this RequestStatus status) => status switch
    {
        RequestStatus.InProgress => "In Progress",
        _ => status.ToString()
    };

    public static string Plain(this RequestStatus status) => status switch
    {
        RequestStatus.New => "We got it",
        RequestStatus.Assigned => "Someone is on it",
        RequestStatus.InProgress => "Being fixed",
        RequestStatus.Resolved => "Fixed",
        _ => "All done"
    };

    public static string CssClass(this RequestStatus status) => status switch
    {
        RequestStatus.InProgress => "status-in-progress",
        _ => "status-" + status.ToString().ToLowerInvariant()
    };

    public static bool IsOpen(this RequestStatus status)
        => status is RequestStatus.New or RequestStatus.Assigned or RequestStatus.InProgress;

    public static RequestStatus? Next(this RequestStatus status) => status switch
    {
        RequestStatus.New => RequestStatus.Assigned,
        RequestStatus.Assigned => RequestStatus.InProgress,
        RequestStatus.InProgress => RequestStatus.Resolved,
        RequestStatus.Resolved => RequestStatus.Closed,
        _ => null
    };
}
