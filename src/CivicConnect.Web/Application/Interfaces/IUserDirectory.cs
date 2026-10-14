using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Application.Interfaces;

public interface IUserDirectory
{
    Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApplicationUser>> ListByRoleAsync(string role, CancellationToken cancellationToken = default);
    Task InsertAsync(ApplicationUser user, CancellationToken cancellationToken = default);
}
