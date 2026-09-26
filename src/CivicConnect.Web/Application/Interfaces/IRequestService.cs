using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Interfaces;

public interface IRequestService
{
    Task<ServiceRequest> CreateAsync(
        string submittedById,
        string title,
        string description,
        int categoryId,
        RequestPriority priority,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> ChangeStatusAsync(
        Guid requestId,
        RequestStatus newStatus,
        string performedById,
        string? notes,
        CancellationToken cancellationToken = default);
}
