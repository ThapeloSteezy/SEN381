using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Application;
using CivicConnect.Web.Domain;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;

namespace CivicConnect.Web.Application.Services;

public sealed class RequestService : IRequestService
{
    private readonly IRequestStore _store;
    private readonly IUserDirectory _users;
    private readonly IStatusTransitionPolicy _transitionPolicy;

    public RequestService(IRequestStore store, IUserDirectory users, IStatusTransitionPolicy transitionPolicy)
    {
        _store = store;
        _users = users;
        _transitionPolicy = transitionPolicy;
    }

    public Task<IReadOnlyList<ServiceRequest>> ListVisibleAsync(string userId, bool seesAll, CancellationToken cancellationToken = default)
        => _store.GetVisibleToUserAsync(userId, seesAll, cancellationToken);

    public Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        => _store.GetActiveCategoriesAsync(cancellationToken);

    public Task<ServiceRequest?> GetAsync(Guid id, CancellationToken cancellationToken = default)
        => _store.GetByIdAsync(id, cancellationToken);

    public async Task<IReadOnlyList<UserUpdate>> ListUpdatesAsync(string userId, CancellationToken cancellationToken = default)
    {
        var requests = await _store.GetWithFeedbackForAsync(userId, cancellationToken);
        return requests
            .SelectMany(request => request.Feedback
                .Where(item => item.RecipientUserId == userId)
                .Select(item => new UserUpdate(request, item)))
            .OrderBy(update => update.Feedback.IsRead)
            .ThenByDescending(update => update.Feedback.CreatedAtUtc)
            .Take(30)
            .ToList();
    }

    public Task<long> CountUnreadUpdatesAsync(string userId, CancellationToken cancellationToken = default)
        => _store.CountWithUnreadFeedbackForAsync(userId, cancellationToken);

    public Task MarkUpdatesReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
        => _store.MarkFeedbackReadAsync(requestId, userId, cancellationToken);

    public async Task<ServiceRequest> CreateAsync(
        UserRef submittedBy,
        string title,
        string description,
        int categoryId,
        string roomId,
        RequestPriority priority,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Tell us what is wrong.", nameof(description));

        var category = await _store.GetCategoryByIdAsync(categoryId, cancellationToken);
        if (category is null || !category.IsActive)
            throw new ArgumentException("Pick what kind of problem it is.", nameof(categoryId));

        var room = FacilityPlan.Find(roomId)
            ?? throw new ArgumentException("Pick the room.", nameof(roomId));

        if (string.IsNullOrWhiteSpace(title))
            title = TitleFrom(description);

        var now = DateTime.UtcNow;
        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            Description = description.Trim(),
            CategoryId = category.Id,
            Category = new RequestCategory { Id = category.Id, Name = category.Name, IsActive = category.IsActive },
            RoomId = room.Id,
            Priority = priority,
            Status = RequestStatus.New,
            SubmittedById = submittedBy.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            Version = 1
        };
        request.History.Add(new RequestHistory
        {
            ActionType = "Submitted",
            ToStatus = RequestStatus.New,
            PerformedBy = submittedBy,
            PerformedAtUtc = now
        });

        const int attempts = 8;
        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            request.RequestNumber = $"CC-{now:yyyyMMdd}-{Random.Shared.Next(1000, 10000)}";
            try
            {
                await _store.InsertAsync(request, cancellationToken);
                return request;
            }
            catch (DuplicateRequestNumberException)
            {
                if (attempt == attempts)
                    throw new InvalidOperationException("Could not save the request. Please try again.");
                request.Id = Guid.NewGuid();
            }
        }

        throw new InvalidOperationException("Could not save the request. Please try again.");
    }

    public async Task<(bool Success, string? Error)> AssignAsync(
        Guid requestId,
        RequestStatus expectedStatus,
        string staffUserId,
        UserRef supervisor,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var request = await _store.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
            return (false, "Request not found.");

        if (request.Status != expectedStatus)
            return (false, $"Not changed. This request was already moved to {request.Status.Label()} by someone else.");

        if (!request.Status.IsOpen())
            return (false, "Not changed. Fixed and closed requests cannot be given to someone else.");

        var staff = string.IsNullOrWhiteSpace(staffUserId) ? null : await _users.FindByIdAsync(staffUserId, cancellationToken);
        if (staff is null || staff.Role != UserRoles.Staff)
            return (false, "Pick a staff member.");

        if (request.AssignedToId == staff.Id)
            return (false, $"Not changed. This request is already with {staff.DisplayName}.");

        var previousVersion = request.Version;
        var now = DateTime.UtcNow;
        var assignee = new UserRef { Id = staff.Id, Email = staff.Email, Name = staff.DisplayName };
        var firstAssignment = request.Status == RequestStatus.New;

        request.History.Add(new RequestHistory
        {
            ActionType = firstAssignment ? "Assigned" : "Reassigned",
            FromStatus = request.Status,
            ToStatus = firstAssignment ? RequestStatus.Assigned : request.Status,
            AssignedTo = assignee,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PerformedBy = supervisor,
            PerformedAtUtc = now
        });
        if (firstAssignment)
        {
            request.Status = RequestStatus.Assigned;
            request.Feedback.Add(new RequestFeedback
            {
                RecipientUserId = request.SubmittedById,
                Message = $"{staff.DisplayName} will fix your request.",
                CreatedAtUtc = now
            });
        }
        request.Feedback.Add(new RequestFeedback
        {
            RecipientUserId = staff.Id,
            Message = $"{supervisor.Label} gave you a new job.",
            CreatedAtUtc = now
        });
        request.AssignedToId = assignee.Id;
        request.AssignedTo = assignee;
        request.UpdatedAtUtc = now;

        var saved = await _store.TryUpdateAsync(request, previousVersion, cancellationToken);
        return saved ? (true, null) : (false, "Not changed. Someone else updated this request at the same time. Try again.");
    }

    public async Task<(bool Success, string? Error)> ChangeStatusAsync(
        Guid requestId,
        RequestStatus expectedStatus,
        RequestStatus newStatus,
        UserRef actor,
        bool actorIsSupervisor,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var request = await _store.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
            return (false, "Request not found.");

        var oldStatus = request.Status;
        if (oldStatus != expectedStatus)
            return (false, $"Not changed. This request was already moved to {oldStatus.Label()} by someone else.");

        if (oldStatus == newStatus)
            return (false, $"Not changed. This request is already {oldStatus.Label()}.");

        if (!_transitionPolicy.IsAllowed(oldStatus, newStatus))
            return (false, NotAllowedMessage(oldStatus));

        if (newStatus == RequestStatus.Assigned)
            return (false, "Not changed. A supervisor assigns a request by picking a staff member.");

        if (!actorIsSupervisor)
        {
            if (request.AssignedToId != actor.Id)
                return (false, "Not changed. Only the person this request is assigned to can update it.");
            if (newStatus == RequestStatus.Closed)
                return (false, "Not changed. A supervisor closes the request after checking it.");
        }

        var previousVersion = request.Version;
        var now = DateTime.UtcNow;
        request.Status = newStatus;
        request.UpdatedAtUtc = now;
        request.History.Add(new RequestHistory
        {
            ActionType = "StatusChanged",
            FromStatus = oldStatus,
            ToStatus = newStatus,
            Notes = string.IsNullOrWhiteSpace(notes) ? null : notes.Trim(),
            PerformedBy = actor,
            PerformedAtUtc = now
        });
        request.Feedback.Add(new RequestFeedback
        {
            RecipientUserId = request.SubmittedById,
            Message = newStatus switch
            {
                RequestStatus.InProgress => "Work has started on your request.",
                RequestStatus.Resolved => "Your request has been fixed.",
                RequestStatus.Closed => "Your request is closed.",
                _ => $"Your request moved to {newStatus.Label()}."
            },
            CreatedAtUtc = now
        });

        if (newStatus == RequestStatus.Resolved
            && request.History.LastOrDefault(h => h.ActionType is "Assigned" or "Reassigned")?.PerformedBy is { } assigner
            && assigner.Id != actor.Id)
        {
            request.Feedback.Add(new RequestFeedback
            {
                RecipientUserId = assigner.Id,
                Message = $"{actor.Label} fixed this. It is ready to close.",
                CreatedAtUtc = now
            });
        }

        var saved = await _store.TryUpdateAsync(request, previousVersion, cancellationToken);
        return saved ? (true, null) : (false, "Not changed. Someone else updated this request at the same time. Try again.");
    }

    private static string TitleFrom(string description)
    {
        var text = description.Trim();
        var end = text.IndexOfAny(['.', '!', '?', '\n']);
        if (end > 0) text = text[..end];
        return text.Length <= 80 ? text.Trim() : text[..77].TrimEnd() + "...";
    }

    private static string NotAllowedMessage(RequestStatus from)
        => from.Next() is { } next
            ? $"Not changed. A request that is {from.Label()} can only move to {next.Label()}."
            : $"Not changed. {from.Label()} requests cannot be changed.";
}
