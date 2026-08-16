using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace SecureBox.Infrastructure.Services;

public interface ITokenStore
{
    Task StoreRefreshTokenAsync(Guid userId, string jti, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> RefreshTokenExistsAsync(Guid userId, string jti, CancellationToken cancellationToken = default);
    Task RevokeRefreshTokenAsync(Guid userId, string jti, CancellationToken cancellationToken = default);
    Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default);
    Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default);
    Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken cancellationToken = default);
}

public sealed class MemoryTokenStore : ITokenStore
{
    private readonly ConcurrentDictionary<string, DateTimeOffset> _entries = new();

    public Task StoreRefreshTokenAsync(Guid userId, string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _entries[$"refresh:{userId:N}:{jti}"] = DateTimeOffset.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> RefreshTokenExistsAsync(Guid userId, string jti, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsAlive($"refresh:{userId:N}:{jti}"));
    }

    public Task RevokeRefreshTokenAsync(Guid userId, string jti, CancellationToken cancellationToken = default)
    {
        _entries.TryRemove($"refresh:{userId:N}:{jti}", out _);
        return Task.CompletedTask;
    }

    public Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var prefix = $"refresh:{userId:N}:";
        foreach (var key in _entries.Keys.Where(k => k.StartsWith(prefix, StringComparison.Ordinal)))
        {
            _entries.TryRemove(key, out _);
        }

        return Task.CompletedTask;
    }

    public Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        _entries[$"blacklist:{jti}"] = DateTimeOffset.UtcNow.Add(ttl);
        return Task.CompletedTask;
    }

    public Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(IsAlive($"blacklist:{jti}"));
    }

    private bool IsAlive(string key)
    {
        if (!_entries.TryGetValue(key, out var expires))
        {
            return false;
        }

        if (expires > DateTimeOffset.UtcNow)
        {
            return true;
        }

        _entries.TryRemove(key, out _);
        return false;
    }
}

public sealed class RedisTokenStore : ITokenStore
{
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisTokenStore> _logger;

    public RedisTokenStore(IConnectionMultiplexer redis, ILogger<RedisTokenStore> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task StoreRefreshTokenAsync(Guid userId, string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        try
        {
            await _redis.GetDatabase().StringSetAsync(RefreshKey(userId, jti), "1", ttl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to store refresh token for {UserId}", userId);
            throw;
        }
    }

    public async Task<bool> RefreshTokenExistsAsync(Guid userId, string jti, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redis.GetDatabase().KeyExistsAsync(RefreshKey(userId, jti));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check refresh token for {UserId}", userId);
            return false;
        }
    }

    public async Task RevokeRefreshTokenAsync(Guid userId, string jti, CancellationToken cancellationToken = default)
    {
        await _redis.GetDatabase().KeyDeleteAsync(RefreshKey(userId, jti));
    }

    public async Task RevokeAllRefreshTokensAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var server = _redis.GetServers().FirstOrDefault(s => s.IsConnected);
        if (server is null)
        {
            return;
        }

        var keys = server.Keys(pattern: $"SecureBox:refresh:{userId:N}:*").ToArray();
        if (keys.Length > 0)
        {
            await _redis.GetDatabase().KeyDeleteAsync(keys);
        }
    }

    public async Task BlacklistAccessTokenAsync(string jti, TimeSpan ttl, CancellationToken cancellationToken = default)
    {
        await _redis.GetDatabase().StringSetAsync($"SecureBox:blacklist:{jti}", "1", ttl);
    }

    public async Task<bool> IsAccessTokenBlacklistedAsync(string jti, CancellationToken cancellationToken = default)
    {
        try
        {
            return await _redis.GetDatabase().KeyExistsAsync($"SecureBox:blacklist:{jti}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check access-token blacklist");
            return false;
        }
    }

    private static RedisKey RefreshKey(Guid userId, string jti) => $"SecureBox:refresh:{userId:N}:{jti}";
}
