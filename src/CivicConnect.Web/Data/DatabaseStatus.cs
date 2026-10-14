namespace CivicConnect.Web.Data;

public sealed class DatabaseStatus
{
    public bool Ready { get; set; }
    public string? Error { get; set; }
}
