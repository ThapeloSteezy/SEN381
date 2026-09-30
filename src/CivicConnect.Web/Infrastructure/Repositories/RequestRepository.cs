using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Web.Infrastructure.Repositories;

public sealed class RequestRepository : IRequestRepository
{
    private readonly ApplicationDbContext _db;

    public RequestRepository(ApplicationDbContext db) => _db = db;

    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _db.ServiceRequests
            .Include(r => r.Category)
            .Include(r => r.SubmittedBy)
            .Include(r => r.AssignedTo)
            .Include(r => r.History.OrderByDescending(h => h.PerformedAtUtc))
                .ThenInclude(h => h.PerformedBy)
            .Include(r => r.Feedback.OrderByDescending(f => f.CreatedAtUtc))
            .AsSplitQuery()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<List<ServiceRequest>> GetVisibleToUserAsync(
        string userId,
        bool staffAccess,
        CancellationToken cancellationToken = default)
    {
        IQueryable<ServiceRequest> query = _db.ServiceRequests
            .Include(r => r.Category)
            .AsNoTracking();

        if (!staffAccess)
            query = query.Where(r => r.SubmittedById == userId);

        return query.OrderByDescending(r => r.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public Task<List<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        => _db.RequestCategories
            .Where(c => c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public Task AddAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        => _db.ServiceRequests.AddAsync(request, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
        => _db.SaveChangesAsync(cancellationToken);
}
