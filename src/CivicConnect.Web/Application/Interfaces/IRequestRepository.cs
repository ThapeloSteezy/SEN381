using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Application.Interfaces;

public interface IRequestRepository
{
    Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<List<ServiceRequest>> GetVisibleToUserAsync(string userId, bool staffAccess, CancellationToken cancellationToken = default);
    Task<List<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
