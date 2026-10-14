using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;
using MongoDB.Driver;

namespace CivicConnect.Web.Data;

public sealed class MongoUserDirectory : IUserDirectory
{
    private readonly MongoContext _mongo;

    public MongoUserDirectory(MongoContext mongo) => _mongo = mongo;

    public Task<ApplicationUser?> FindByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = email.Trim().ToLowerInvariant();
        return _mongo.Users.Find(user => user.Email == normalized).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken cancellationToken = default)
        => _mongo.Users.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<ApplicationUser>> ListByRoleAsync(string role, CancellationToken cancellationToken = default)
        => await _mongo.Users.Find(user => user.Role == role).SortBy(user => user.DisplayName).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ApplicationUser>> ListAllAsync(CancellationToken cancellationToken = default)
        => await _mongo.Users.Find(Builders<ApplicationUser>.Filter.Empty).SortBy(user => user.DisplayName).ToListAsync(cancellationToken);

    public Task SetPasswordHashAsync(string id, string passwordHash, CancellationToken cancellationToken = default)
        => _mongo.Users.UpdateOneAsync(
            user => user.Id == id,
            Builders<ApplicationUser>.Update.Set(user => user.PasswordHash, passwordHash),
            cancellationToken: cancellationToken);

    public Task InsertAsync(ApplicationUser user, CancellationToken cancellationToken = default)
        => _mongo.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
}
