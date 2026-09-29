using System.Diagnostics.Metrics;
namespace Andalos.API.Security;
public static class SecurityMetrics
{
    public static readonly Meter Meter = new("Andalos.Authorization", "1.0.0");
    public static readonly Counter<long> IdentityQueries = Meter.CreateCounter<long>("authorization.identity_queries");
    public static readonly Counter<long> UnionQueries = Meter.CreateCounter<long>("authorization.union_queries");
    public static readonly Counter<long> CacheHits = Meter.CreateCounter<long>("authorization.cache_hits");
    public static readonly Counter<long> CacheMisses = Meter.CreateCounter<long>("authorization.cache_misses");
    public static readonly Histogram<double> DecisionDuration = Meter.CreateHistogram<double>("authorization.decision_ms", "ms");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("authorization.duration_ms", "ms");
}
