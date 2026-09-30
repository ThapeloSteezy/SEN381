using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Application.Services;

public sealed class FeedbackService : IFeedbackService
{
    private readonly ApplicationDbContext _db;

    public FeedbackService(ApplicationDbContext db) => _db = db;

    public Task AddUpdateFeedbackAsync(ServiceRequest request, string message, CancellationToken cancellationToken = default)
    {
        _db.RequestFeedback.Add(new RequestFeedback
        {
            ServiceRequestId = request.Id,
            RecipientUserId = request.SubmittedById,
            Message = message,
            CreatedAtUtc = DateTime.UtcNow,
            IsRead = false
        });
        return Task.CompletedTask;
    }
}
