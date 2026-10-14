using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Data;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Infrastructure.Repositories;

public sealed class AuditWriter : IAuditWriter
{
    private readonly ApplicationDbContext _db;

    public AuditWriter(ApplicationDbContext db) => _db = db;

    public Task WriteStatusChangeAsync(
        ServiceRequest request,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string performedById,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        _db.RequestHistory.Add(new RequestHistory
        {
            ServiceRequestId = request.Id,
            ActionType = "StatusChanged",
            FromStatus = fromStatus,
            ToStatus = toStatus,
            Notes = notes,
            PerformedById = performedById,
            PerformedAtUtc = DateTime.UtcNow
        });

        return Task.CompletedTask;
    }
}
