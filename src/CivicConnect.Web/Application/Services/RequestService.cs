using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CivicConnect.Web.Application.Services;

public sealed class RequestService : IRequestService
{
    private readonly IRequestRepository _repository;
    private readonly IAuditWriter _auditWriter;
    private readonly IFeedbackService _feedbackService;
    private readonly IStatusTransitionPolicy _transitionPolicy;
    private readonly IUnitOfWork _unitOfWork;

    public RequestService(
        IRequestRepository repository,
        IAuditWriter auditWriter,
        IFeedbackService feedbackService,
        IStatusTransitionPolicy transitionPolicy,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _auditWriter = auditWriter;
        _feedbackService = feedbackService;
        _transitionPolicy = transitionPolicy;
        _unitOfWork = unitOfWork;
    }

    public async Task<ServiceRequest> CreateAsync(
        string submittedById,
        string title,
        string description,
        int categoryId,
        RequestPriority priority,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("A request title is required.", nameof(title));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("A request description is required.", nameof(description));

        var request = new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = $"CC-{DateTime.UtcNow:yyyyMMdd}-{Random.Shared.Next(1000, 9999)}",
            Title = title.Trim(),
            Description = description.Trim(),
            CategoryId = categoryId,
            Priority = priority,
            Status = RequestStatus.New,
            SubmittedById = submittedById,
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow,
            Version = 1
        };

        await _repository.AddAsync(request, cancellationToken);
        await _repository.SaveChangesAsync(cancellationToken);
        return request;
    }

    public async Task<(bool Success, string? Error)> ChangeStatusAsync(
        Guid requestId,
        RequestStatus newStatus,
        string performedById,
        string? notes,
        CancellationToken cancellationToken = default)
    {
        var request = await _repository.GetByIdAsync(requestId, cancellationToken);
        if (request is null)
            return (false, "The request could not be found.");

        var oldStatus = request.Status;
        if (!_transitionPolicy.IsAllowed(oldStatus, newStatus))
            return (false, $"The transition from {oldStatus} to {newStatus} is not permitted.");

        if (oldStatus == newStatus)
            return (true, null);

        request.Status = newStatus;
        request.UpdatedAtUtc = DateTime.UtcNow;
        request.Version++;

        try
        {
            await _auditWriter.WriteStatusChangeAsync(
                request, oldStatus, newStatus, performedById, notes, cancellationToken);

            await _feedbackService.AddUpdateFeedbackAsync(
                request,
                $"Your request status changed from {oldStatus} to {newStatus}.",
                cancellationToken);

            await _unitOfWork.ExecuteInTransactionAsync(
                () => _repository.SaveChangesAsync(cancellationToken),
                cancellationToken);
            return (true, null);
        }
        catch (DbUpdateConcurrencyException)
        {
            return (false, "The request was changed by another user. Reload the request and try again.");
        }
    }
}
