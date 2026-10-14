using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Application.Interfaces;

public interface IFeedbackService
{
    Task AddUpdateFeedbackAsync(ServiceRequest request, string message, CancellationToken cancellationToken = default);
}
