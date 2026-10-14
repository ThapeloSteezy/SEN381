using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Application.Interfaces;

public interface IRequestStore
{
    Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRequest>> GetVisibleToUserAsync(string userId, bool staffAccess, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<RequestCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default);
    Task InsertAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task<bool> TryUpdateAsync(ServiceRequest request, long previousVersion, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ServiceRequest>> GetWithFeedbackForAsync(string userId, CancellationToken cancellationToken = default);
    Task<long> CountWithUnreadFeedbackForAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkFeedbackReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);
    Task EnsureCategoriesAsync(IReadOnlyList<RequestCategory> categories, CancellationToken cancellationToken = default);
}
