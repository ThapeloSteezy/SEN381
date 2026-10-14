namespace CivicConnect.Web.Domain.Entities;

public class RequestFeedback
{
    public string Message { get; set; } = string.Empty;
    public string RecipientUserId { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public bool IsRead { get; set; }
}
