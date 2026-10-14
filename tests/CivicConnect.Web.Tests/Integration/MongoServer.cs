using EphemeralMongo;
using MongoDB.Driver;

namespace CivicConnect.Web.Tests.Integration;

// One real mongod process for the whole test run; every test class gets its own database on it.
public sealed class MongoServer : IAsyncLifetime
{
    private IMongoRunner? _runner;

    public async Task InitializeAsync()
        => _runner = await MongoRunner.RunAsync(new MongoRunnerOptions
        {
            Version = MongoVersion.V8,
            AdditionalArguments = ["--quiet"]
        });

    public string ConnectionStringFor(string databaseName)
        => new MongoUrlBuilder(_runner!.ConnectionString) { DatabaseName = databaseName }.ToString();

    public Task DisposeAsync()
    {
        _runner?.Dispose();
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class MongoTests : ICollectionFixture<MongoServer>
{
    public const string Name = "MongoDB";
}
