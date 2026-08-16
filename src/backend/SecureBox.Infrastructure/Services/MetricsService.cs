using Microsoft.EntityFrameworkCore;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class MetricsService : IMetricsService
{
    private readonly SecureBoxDbContext _dbContext;

    public MetricsService(SecureBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, bool isAdmin)
    {
        var keys = _dbContext.Keys.AsNoTracking().AsQueryable();
        if (!isAdmin)
        {
            keys = keys.Where(k => k.OwnerUserId == userId);
        }

        var totalKeys = await keys.CountAsync();
        var activeKeys = await keys.CountAsync(k => k.Status == "Active");
        var expiredKeys = await keys.CountAsync(k => k.Status == "Expired");
        var revokedKeys = await keys.CountAsync(k => k.Status == "Revoked");
        var totalCertificates = await _dbContext.Certificates.CountAsync();
        var totalUsers = isAdmin ? await _dbContext.Users.CountAsync() : 1;

        var byEnvironment = await keys
            .GroupBy(k => k.EnvironmentTag)
            .Select(g => new EnvironmentCountDto(g.Key, g.Count()))
            .ToListAsync();

        var activityQuery = _dbContext.AuditTrails.AsNoTracking().Include(a => a.User).AsQueryable();
        if (!isAdmin)
        {
            activityQuery = activityQuery.Where(a => a.UserId == userId);
        }

        var recent = await activityQuery
            .OrderByDescending(a => a.Timestamp)
            .Take(10)
            .Select(a => new RecentActivityDto(
                a.Action,
                a.Resource,
                a.Timestamp,
                a.User != null ? a.User.Username : "system"))
            .ToListAsync();

        var horizon = DateTime.UtcNow.AddDays(30);
        var now = DateTime.UtcNow;
        var expiringKeys = await keys.CountAsync(k =>
            k.Status == "Active" && k.ExpiresAt != null && k.ExpiresAt <= horizon && k.ExpiresAt > now);
        var certQuery = _dbContext.Certificates.AsNoTracking().AsQueryable();
        var expiringCerts = await certQuery.CountAsync(c =>
            c.Status == "Active" && c.NotAfter <= horizon && c.NotAfter > now);

        var upcomingKeys = await keys
            .Where(k => k.Status == "Active" && k.ExpiresAt != null && k.ExpiresAt <= horizon && k.ExpiresAt > now)
            .OrderBy(k => k.ExpiresAt)
            .Take(10)
            .Select(k => new ExpiryWarningDto("Key", k.KeyId, k.Name, k.ExpiresAt!.Value, (int)(k.ExpiresAt.Value - now).TotalDays))
            .ToListAsync();

        var upcomingCerts = await certQuery
            .Where(c => c.Status == "Active" && c.NotAfter <= horizon && c.NotAfter > now)
            .OrderBy(c => c.NotAfter)
            .Take(10)
            .Select(c => new ExpiryWarningDto("Certificate", c.CertificateId, c.Name, c.NotAfter, (int)(c.NotAfter - now).TotalDays))
            .ToListAsync();

        var upcoming = upcomingKeys.Concat(upcomingCerts).OrderBy(x => x.ExpiresAt).Take(15).ToList();

        return new DashboardStatsDto(
            totalKeys,
            activeKeys,
            expiredKeys,
            revokedKeys,
            totalCertificates,
            totalUsers,
            byEnvironment,
            recent,
            expiringKeys,
            expiringCerts,
            upcoming);
    }
}
