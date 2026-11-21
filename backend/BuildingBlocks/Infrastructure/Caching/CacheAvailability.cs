using System.Diagnostics.Metrics;

namespace UGTM.BuildingBlocks.Infrastructure.Caching;

/// <summary>Shared across request scopes so an outage does not impose a timeout on every cache operation.</summary>
public sealed class CacheAvailability(TimeProvider? timeProvider = null)
{
    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;
    private long _retryAfter;
    private readonly SemaphoreSlim[] _fillLocks = Enumerable.Range(0, 128).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    internal SemaphoreSlim VersionLock { get; } = new(1, 1);
    internal SemaphoreSlim FillLock(string key) => _fillLocks[(StringComparer.Ordinal.GetHashCode(key) & int.MaxValue) % _fillLocks.Length];
    internal static readonly Meter Meter = new("UGTM.Cache");
    internal static readonly Counter<long> Hits = Meter.CreateCounter<long>("ugtm.cache.hits");
    internal static readonly Counter<long> Misses = Meter.CreateCounter<long>("ugtm.cache.misses");
    internal static readonly Counter<long> Failures = Meter.CreateCounter<long>("ugtm.cache.failures");
    internal static readonly Counter<long> Bypasses = Meter.CreateCounter<long>("ugtm.cache.bypasses");

    public bool IsAvailable => _time.GetUtcNow().Ticks >= Interlocked.Read(ref _retryAfter);

    public void BackOff(TimeSpan duration)
    {
        var until = _time.GetUtcNow().Add(duration).Ticks;
        long previous;
        do
        {
            previous = Interlocked.Read(ref _retryAfter);
            if (previous >= until) return;
        } while (Interlocked.CompareExchange(ref _retryAfter, until, previous) != previous);
    }
}
