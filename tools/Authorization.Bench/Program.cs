using Andalos.API.Tests;
using Andalos.API.Data;
using Andalos.API.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Data.SqlClient;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
// ONLY disposable generated SQL database. Neither application tokens nor production accounts are exported.
var connection = Environment.GetEnvironmentVariable("ANDALOS_TEST_SQL") ?? throw new InvalidOperationException("ANDALOS_TEST_SQL is required.");
var usersCount = args.Length > 0 ? int.Parse(args[0]) : 100;
var requests = args.Length > 1 ? int.Parse(args[1]) : 10000;
var concurrency = args.Length > 2 ? int.Parse(args[2]) : 32;
if (usersCount is < 1 or > 5000 || requests is < 1 or > 1000000 || concurrency is < 1 or > 256) throw new ArgumentException("Invalid load bounds.");
var cs = new SqlConnectionStringBuilder(connection) { InitialCatalog = "AndalosAuthorizationTests_Bench_" + Guid.NewGuid().ToString("N") };
using var nodeA = new ApiFactory(cs.ConnectionString); using var nodeB = new ApiFactory(cs.ConnectionString);
var counters = new ConcurrentDictionary<string, long>();
var decisions = new ConcurrentBag<double>(); var checks = new ConcurrentBag<double>(); var httpTimes = new ConcurrentBag<double>();
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) => { if (instrument.Meter.Name == "Andalos.Authorization") l.EnableMeasurementEvents(instrument); };
listener.SetMeasurementEventCallback<long>((i, value, _, _) => counters.AddOrUpdate(i.Name, value, (_, old) => old + value));
listener.SetMeasurementEventCallback<double>((i, value, _, _) => { if (i.Name == "authorization.decision_ms") decisions.Add(value); if (i.Name == "authorization.duration_ms") checks.Add(value); });
listener.Start();
var clients = new List<(HttpClient A, HttpClient B, int Id)>();
try
{
    await nodeA.InitializeDatabaseAsync();
    for (var i = 0; i < usersCount; i++)
    {
        var u = await nodeA.EmployeeAsync(UserRole.Admin, "Units.View"); clients.Add((nodeA.Client(u), nodeB.Client(u), u.Id));
    }
    // Warm both caches. The metric counters below exclude setup and warmup.
    foreach (var pair in clients) { (await pair.A.GetAsync("/api/Units")).EnsureSuccessStatusCode(); (await pair.B.GetAsync("/api/Units")).EnsureSuccessStatusCode(); }
    counters.Clear(); while (decisions.TryTake(out _)) { } while (checks.TryTake(out _)) { }
    await Parallel.ForEachAsync(Enumerable.Range(0, requests), new ParallelOptions { MaxDegreeOfParallelism = concurrency }, async (i, ct) =>
    {
        var pair = clients[i % clients.Count]; var client = i % 2 == 0 ? pair.A : pair.B; var start = Stopwatch.GetTimestamp();
        using var response = await client.GetAsync("/api/Units", ct); response.EnsureSuccessStatusCode(); httpTimes.Add(Stopwatch.GetElapsedTime(start).TotalMilliseconds);
    });
    var steadyCounters = counters.ToDictionary(x => x.Key, x => x.Value);
    var auth = Quantiles(decisions); var permissionCheck = Quantiles(checks); var http = Quantiles(httpTimes);
    var first = clients[0];
    // Revoke using the real context invalidation path; return from SaveChanges is after commit.
    await nodeA.ChangeAsync(db => db.DirectUserPermissions.Remove(db.DirectUserPermissions.Single(x => x.UserId == first.Id)));
    var committed = Stopwatch.GetTimestamp();
    var results = await Task.WhenAll(first.A.GetAsync("/api/Units"), first.B.GetAsync("/api/Units"));
    var revokeToDeniedMs = Stopwatch.GetElapsedTime(committed).TotalMilliseconds;
    if (results.Any(r => r.StatusCode != HttpStatusCode.Forbidden)) throw new Exception("Post-commit authorization stale on a replica.");
    Console.WriteLine(JsonSerializer.Serialize(new
    {
        runtime = Environment.Version.ToString(), database = "SQL Server disposable; two API test hosts", usersCount, requests, concurrency,
        authorizationDecisionMs = auth, permissionCheckMs = permissionCheck, inProcessHttpMs = http,
        steadyCounters, cacheHitRatio = steadyCounters.GetValueOrDefault("authorization.cache_hits") / (double)Math.Max(1, steadyCounters.GetValueOrDefault("authorization.cache_hits") + steadyCounters.GetValueOrDefault("authorization.cache_misses")),
        revokeCommitToBothDeniedMs = revokeToDeniedMs, postCommitStatuses = results.Select(r => (int)r.StatusCode),
        note = "Authorization logical query counters exclude business/audit SQL. In-process HTTP excludes real network, TLS and external proxy. Not production capacity."
    }, new JsonSerializerOptions { WriteIndented = true }));
}
finally
{
    foreach (var c in clients) { c.A.Dispose(); c.B.Dispose(); }
    using var scope = nodeA.Services.CreateScope(); await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureDeletedAsync();
}
static object Quantiles(IEnumerable<double> values)
{
    var a = values.Order().ToArray();
    double P(double p) => a.Length == 0 ? double.NaN : a[Math.Clamp((int)Math.Ceiling(a.Length * p) - 1, 0, a.Length - 1)];
    return new { count = a.Length, p50 = P(.50), p95 = P(.95), p99 = P(.99) };
}
