namespace CivicConnect.Web.Domain.Entities;

public class RequestCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;

    public ICollection<ServiceRequest> Requests { get; set; } = new List<ServiceRequest>();
}
