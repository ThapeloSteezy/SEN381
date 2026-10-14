using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using Xunit.Abstractions;
using CivicConnect.Web.Domain.Entities;
using CivicConnect.Web.Domain.Enums;
using CivicConnect.Web.Tests.Integration;
using MongoDB.Driver;

namespace CivicConnect.Web.Tests.Performance;

[Trait("Category", "Performance")]
[Trait("Requirement", "NFR-01")]
public sealed class AllRequestsLoadTests(MongoServer server, ITestOutputHelper output) : IntegrationTest(server)
{
    [Fact]
    public async Task Supervisor_all_requests_search_stays_responsive_with_500_rows()
    {
        var now = DateTime.UtcNow;
        var rows = Enumerable.Range(0, 500).Select(i => new ServiceRequest
        {
            Id = Guid.NewGuid(),
            RequestNumber = $"CC-LOAD-{i:0000}",
            Title = i % 2 == 0 ? "Kitchen tap drip" : "Hall light out",
            Description = "Seeded for the M3 load test.",
            CategoryId = 5,
            RoomId = i % 2 == 0 ? "kitchen" : "hall",
            Status = RequestStatus.New,
            SubmittedById = CivicConnectApp.Requester.Id,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        }).ToList();
        await App.Mongo.Requests.InsertManyAsync(rows);

        var warmup = await App.SignInAsync(CivicConnectApp.Supervisor);
        Assert.Equal(HttpStatusCode.OK, (await warmup.GetAsync("/Requests?q=kitchen")).StatusCode);

        var times = new ConcurrentBag<long>();
        var errors = 0;
        await Parallel.ForEachAsync(Enumerable.Range(0, 10), new ParallelOptions { MaxDegreeOfParallelism = 10 }, async (_, _) =>
        {
            var browser = await App.SignInAsync(CivicConnectApp.Supervisor);
            for (var i = 0; i < 10; i++)
            {
                var clock = Stopwatch.StartNew();
                var response = await browser.GetAsync("/Requests?q=kitchen");
                clock.Stop();
                times.Add(clock.ElapsedMilliseconds);
                if (response.StatusCode != HttpStatusCode.OK)
                    Interlocked.Increment(ref errors);
            }
        });

        var sorted = times.OrderBy(value => value).ToArray();
        var median = sorted[sorted.Length / 2];
        var p95 = sorted[(int)Math.Clamp(Math.Ceiling(sorted.Length * 0.95) - 1, 0, sorted.Length - 1)];
        output.WriteLine($"All requests search: n={sorted.Length}, median={median}ms, p95={p95}ms, errors={errors}, rows=500, users=10.");
        output.WriteLine("This is a student-scale result on one machine and a throwaway MongoDB. It does not represent municipal production load.");

        Assert.Equal(0, errors);
        Assert.Equal(100, sorted.Length);
        Assert.True(p95 < 5000, $"95th percentile was {p95}ms; expected under 5s on this test host.");
    }
}
