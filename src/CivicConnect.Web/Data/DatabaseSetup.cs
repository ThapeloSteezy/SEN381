using CivicConnect.Web.Application.Interfaces;
using CivicConnect.Web.Domain.Entities;

namespace CivicConnect.Web.Data;

public static class DatabaseSetup
{
    private static readonly RequestCategory[] Categories =
    [
        new() { Id = 1, Name = "Facility Fault" },
        new() { Id = 2, Name = "Damaged Equipment" },
        new() { Id = 3, Name = "Security Concern" },
        new() { Id = 4, Name = "IT Support" },
        new() { Id = 5, Name = "Maintenance" },
        new() { Id = 6, Name = "Lost Property" }
    ];

    public static async Task RunAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await services.GetRequiredService<MongoContext>().EnsureIndexesAsync(cancellationToken);
        await services.GetRequiredService<IRequestStore>().EnsureCategoriesAsync(Categories, cancellationToken);
    }
}
