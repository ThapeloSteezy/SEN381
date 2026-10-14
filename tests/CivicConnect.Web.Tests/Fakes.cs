using CivicConnect.Web.Application;
using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Tests;

internal sealed class InMemoryUsers(params ApplicationUser[] users) : IUserDirectory
{
    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
        => Task.FromResult(users.FirstOrDefault(u => u.Email == email));

    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => Task.FromResult(users.FirstOrDefault(u => u.Id == id));

    public Task<IReadOnlyList<ApplicationUser>> ListByRoleAsync(string role, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ApplicationUser>>(users.Where(u => u.Role == role).ToList());

    public Task InsertAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class InMemoryStore : IRequestStore
{
    public List<ServiceRequest> Requests { get; } = [];
    public int Updates { get; private set; }
    public int InsertAttempts { get; private set; }
    public int FailInserts { get; set; }

    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => Task.FromResult(Requests.FirstOrDefault(r => r.Id == id));

    public Task<IReadOnlyList<ServiceRequest>> GetVisibleToUserAsync(string userId, bool staffAccess, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ServiceRequest>>(Requests.Where(r => staffAccess || r.SubmittedById == userId || r.AssignedToId == userId).ToList());

    public Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<RequestCategory>>([new RequestCategory { Id = 5, Name = "Maintenance" }]);

    public Task<RequestCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        => Task.FromResult<RequestCategory?>(id == 5 ? new RequestCategory { Id = 5, Name = "Maintenance" } : null);

    public Task InsertAsync(ServiceRequest request, CancellationToken cancellationToken = default)
    {
        InsertAttempts++;
        if (FailInserts > 0)
        {
            FailInserts--;
            throw new DuplicateRequestNumberException();
        }

        if (Requests.Any(existing => existing.RequestNumber == request.RequestNumber))
            throw new DuplicateRequestNumberException();

        Requests.Add(request);
        return Task.CompletedTask;
    }

    public Task<bool> TryUpdateAsync(ServiceRequest request, long previousVersion, CancellationToken cancellationToken = default)
    {
        Updates++;
        request.Version = previousVersion + 1;
        return Task.FromResult(true);
    }

    public Task<IReadOnlyList<ServiceRequest>> GetWithFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<ServiceRequest>>(Requests.Where(r => r.Feedback.Any(f => f.RecipientUserId == userId)).ToList());

    public Task<long> CountWithUnreadFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
        => Task.FromResult((long)Requests.Count(r => r.Feedback.Any(f => f.RecipientUserId == userId && !f.IsRead)));

    public Task MarkFeedbackReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
    {
        foreach (var item in Requests.Single(r => r.Id == requestId).Feedback.Where(f => f.RecipientUserId == userId))
            item.IsRead = true;
        return Task.CompletedTask;
    }

    public Task EnsureCategoriesAsync(IReadOnlyList<RequestCategory> categories, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
