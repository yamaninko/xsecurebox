using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SecureBox.Infrastructure.Services;

public interface IRateLimitService
{
    Task<(bool Allowed, int Limit, int Remaining, TimeSpan Reset)> TryAcquireAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default);
}

public sealed class MemoryRateLimitService : IRateLimitService
{
    private readonly ConcurrentDictionary<string, Window> _windows = new();

    public Task<(bool Allowed, int Limit, int Remaining, TimeSpan Reset)> TryAcquireAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var entry = _windows.AddOrUpdate(
            key,
            _ => new Window(now.Add(window), 1),
            (_, existing) =>
            {
                if (existing.ResetAt <= now)
                {
                    return new Window(now.Add(window), 1);
                }

                return existing with { Count = existing.Count + 1 };
            });

        var allowed = entry.Count <= limit;
        var remaining = Math.Max(0, limit - entry.Count);
        var reset = entry.ResetAt - now;
        if (reset < TimeSpan.Zero)
        {
            reset = TimeSpan.Zero;
        }

        return Task.FromResult((allowed, limit, remaining, reset));
    }

    private sealed record Window(DateTimeOffset ResetAt, int Count);
}

public sealed class RedisRateLimitService : IRateLimitService
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisRateLimitService> _logger;

    public RedisRateLimitService(IConnectionMultiplexer redis, ILogger<RedisRateLimitService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<(bool Allowed, int Limit, int Remaining, TimeSpan Reset)> TryAcquireAsync(
        string key,
        int limit,
        TimeSpan window,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var db = _redis.GetDatabase();
            var redisKey = $"SecureBox:ratelimit:{key}";
            var count = await db.StringIncrementAsync(redisKey);
            if (count == 1)
            {
                await db.KeyExpireAsync(redisKey, window);
            }

            var ttl = await db.KeyTimeToLiveAsync(redisKey) ?? window;
            var remaining = Math.Max(0, limit - (int)count);
            return (count <= limit, limit, remaining, ttl);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Rate limit check failed for {Key}; allowing request", key);
            return (true, limit, limit, window);
        }
    }
}
