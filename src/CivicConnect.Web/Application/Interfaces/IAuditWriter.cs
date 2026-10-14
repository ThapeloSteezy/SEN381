using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Interfaces;

public interface IAuditWriter
{
    Task WriteStatusChangeAsync(
        ServiceRequest request,
        RequestStatus fromStatus,
        RequestStatus toStatus,
        string performedById,
        string? notes,
        CancellationToken cancellationToken = default);
}
