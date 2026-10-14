using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Interfaces;

public interface IRequestService
{
    Task<IReadOnlyList<ServiceRequest>> ListVisibleAsync(string userId, bool seesAll, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserUpdate>> ListUpdatesAsync(string userId, CancellationToken cancellationToken = default);
    Task<long> CountUnreadUpdatesAsync(string userId, CancellationToken cancellationToken = default);
    Task MarkUpdatesReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default);

    Task<ServiceRequest> CreateAsync(
        UserRef submittedBy,
        string title,
        string description,
        int categoryId,
        string roomId,
        RequestPriority priority,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> AssignAsync(
        Guid requestId,
        RequestStatus expectedStatus,
        string staffUserId,
        UserRef supervisor,
        string? notes,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string? Error)> ChangeStatusAsync(
        Guid requestId,
        RequestStatus expectedStatus,
        RequestStatus newStatus,
        UserRef actor,
        bool actorIsSupervisor,
        string? notes,
        CancellationToken cancellationToken = default);
}

public sealed record UserUpdate(ServiceRequest Request, RequestFeedback Feedback);
