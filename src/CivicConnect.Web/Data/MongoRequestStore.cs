using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;
using MongoDB.Driver;

namespace CivicConnect.Web.Data;

public sealed class MongoRequestStore : IRequestStore
{
    private readonly MongoContext _mongo;

    public MongoRequestStore(MongoContext mongo) => _mongo = mongo;

    public Task<ServiceRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _mongo.Requests.Find(request => request.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public async Task<IReadOnlyList<ServiceRequest>> GetVisibleToUserAsync(
        string userId,
        bool staffAccess,
        CancellationToken cancellationToken = default)
    {
        var filter = staffAccess
            ? Builders<ServiceRequest>.Filter.Empty
            : Builders<ServiceRequest>.Filter.Or(
                Builders<ServiceRequest>.Filter.Eq(request => request.SubmittedById, userId),
                Builders<ServiceRequest>.Filter.Eq(request => request.AssignedToId, userId));

        return await _mongo.Requests
            .Find(filter)
            .SortByDescending(request => request.CreatedAtUtc)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<RequestCategory>> GetActiveCategoriesAsync(CancellationToken cancellationToken = default)
        => await _mongo.Categories
            .Find(category => category.IsActive)
            .SortBy(category => category.Name)
            .ToListAsync(cancellationToken);

    public Task<RequestCategory?> GetCategoryByIdAsync(int id, CancellationToken cancellationToken = default)
        => _mongo.Categories.Find(category => category.Id == id).FirstOrDefaultAsync(cancellationToken)!;

    public Task InsertAsync(ServiceRequest request, CancellationToken cancellationToken = default)
        => _mongo.Requests.InsertOneAsync(request, cancellationToken: cancellationToken);

    public async Task<bool> TryUpdateAsync(ServiceRequest request, long previousVersion, CancellationToken cancellationToken = default)
    {
        request.Version = previousVersion + 1;
        var result = await _mongo.Requests.ReplaceOneAsync(
            current => current.Id == request.Id && current.Version == previousVersion,
            request,
            cancellationToken: cancellationToken);

        return result.MatchedCount == 1;
    }

    public async Task<IReadOnlyList<ServiceRequest>> GetWithFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
        => await _mongo.Requests
            .Find(Builders<ServiceRequest>.Filter.ElemMatch(request => request.Feedback, item => item.RecipientUserId == userId))
            .SortByDescending(request => request.UpdatedAtUtc)
            .Limit(30)
            .ToListAsync(cancellationToken);

    public Task<long> CountWithUnreadFeedbackForAsync(string userId, CancellationToken cancellationToken = default)
        => _mongo.Requests.CountDocumentsAsync(
            Builders<ServiceRequest>.Filter.ElemMatch(request => request.Feedback, item => item.RecipientUserId == userId && !item.IsRead),
            cancellationToken: cancellationToken);

    // Updates only the read flags so that marking as read never bumps Version or races a status change.
    public Task MarkFeedbackReadAsync(Guid requestId, string userId, CancellationToken cancellationToken = default)
        => _mongo.Requests.UpdateOneAsync(
            request => request.Id == requestId,
            Builders<ServiceRequest>.Update.Set("Feedback.$[mine].IsRead", true),
            new UpdateOptions
            {
                ArrayFilters =
                [
                    new BsonDocumentArrayFilterDefinition<MongoDB.Bson.BsonDocument>(
                        new MongoDB.Bson.BsonDocument { ["mine.RecipientUserId"] = userId, ["mine.IsRead"] = false })
                ]
            },
            cancellationToken);

    public async Task EnsureCategoriesAsync(IReadOnlyList<RequestCategory> categories, CancellationToken cancellationToken = default)
    {
        foreach (var category in categories)
        {
            var exists = await _mongo.Categories.Find(current => current.Name == category.Name).AnyAsync(cancellationToken);
            if (!exists)
                await _mongo.Categories.InsertOneAsync(category, cancellationToken: cancellationToken);
        }
    }
}
