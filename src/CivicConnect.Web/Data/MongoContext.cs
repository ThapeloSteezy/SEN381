using CivicConnect.Web.Domain.Entities;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Driver;

namespace CivicConnect.Web.Data;

public sealed class MongoContext
{
    static MongoContext()
    {
        var pack = new ConventionPack
        {
            new GuidAsStandardConvention()
        };
        ConventionRegistry.Register("civicconnect-guid", pack, _ => true);
    }

    public MongoContext(IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("MongoDb");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Set ConnectionStrings:MongoDb using appsettings or an environment variable.");

        var url = new MongoUrl(connectionString);
        var client = new MongoClient(url);
        Database = client.GetDatabase(url.DatabaseName ?? "civicconnect");
    }

    public IMongoDatabase Database { get; }

    public IMongoCollection<ServiceRequest> Requests => Database.GetCollection<ServiceRequest>("requests");
    public IMongoCollection<RequestCategory> Categories => Database.GetCollection<RequestCategory>("categories");
    public IMongoCollection<ApplicationUser> Users => Database.GetCollection<ApplicationUser>("users");

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        await Users.Indexes.CreateOneAsync(
            new CreateIndexModel<ApplicationUser>(
                Builders<ApplicationUser>.IndexKeys.Ascending(user => user.Email),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
        await Requests.Indexes.CreateOneAsync(
            new CreateIndexModel<ServiceRequest>(
                Builders<ServiceRequest>.IndexKeys.Ascending(request => request.RequestNumber),
                new CreateIndexOptions { Unique = true }),
            cancellationToken: cancellationToken);
    }
}

sealed class GuidAsStandardConvention : ConventionBase, IMemberMapConvention
{
    public void Apply(BsonMemberMap memberMap)
    {
        if (memberMap.MemberType == typeof(Guid))
            memberMap.SetSerializer(new GuidSerializer(GuidRepresentation.Standard));
    }
}
