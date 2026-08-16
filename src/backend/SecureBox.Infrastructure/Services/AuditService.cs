using Microsoft.EntityFrameworkCore;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class AuditService : IAuditService
{
    private readonly SecureBoxDbContext _dbContext;

    public AuditService(SecureBoxDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task LogAuditTrailAsync(AuditTrailDto auditTrail)
    {
        _dbContext.AuditTrails.Add(new AuditTrail
        {
            AuditId = Guid.NewGuid(),
            UserId = auditTrail.UserId,
            Action = auditTrail.Action,
            Resource = auditTrail.Resource,
            ResourceId = auditTrail.ResourceId,
            Details = auditTrail.Details,
            IPAddress = auditTrail.IPAddress,
            UserAgent = auditTrail.UserAgent,
            Timestamp = DateTime.UtcNow,
            Severity = string.IsNullOrWhiteSpace(auditTrail.Severity) ? "Info" : auditTrail.Severity
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task<IEnumerable<AuditTrailListDto>> GetAuditTrailsAsync(AuditQueryParams queryParams)
    {
        var page = Math.Max(1, queryParams.Page);
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

        var query = _dbContext.AuditTrails.AsNoTracking().Include(a => a.User).AsQueryable();

        if (queryParams.UserId.HasValue)
        {
            query = query.Where(a => a.UserId == queryParams.UserId);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Action))
        {
            query = query.Where(a => a.Action == queryParams.Action);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Resource))
        {
            query = query.Where(a => a.Resource == queryParams.Resource);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Severity))
        {
            query = query.Where(a => a.Severity == queryParams.Severity);
        }

        if (queryParams.FromDate.HasValue)
        {
            query = query.Where(a => a.Timestamp >= queryParams.FromDate.Value);
        }

        if (queryParams.ToDate.HasValue)
        {
            query = query.Where(a => a.Timestamp <= queryParams.ToDate.Value);
        }

        var trails = await query
            .OrderByDescending(a => a.Timestamp)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return trails.Select(a => new AuditTrailListDto(
            a.AuditId,
            a.UserId,
            a.User?.Username ?? "system",
            a.Action,
            a.Resource,
            a.ResourceId,
            a.Details,
            a.IPAddress,
            a.UserAgent,
            a.Severity,
            a.Timestamp));
    }

    public async Task<IEnumerable<KeyAccessLogDto>> GetKeyAccessLogsAsync(Guid keyId, Guid? userId)
    {
        var query = _dbContext.KeyAccessLogs
            .AsNoTracking()
            .Include(l => l.User)
            .Where(l => l.KeyId == keyId);

        if (userId.HasValue)
        {
            query = query.Where(l => l.AccessedBy == userId.Value);
        }

        var logs = await query
            .OrderByDescending(l => l.AccessedAt)
            .Take(100)
            .ToListAsync();

        return logs.Select(l => new KeyAccessLogDto(
            l.AccessLogId,
            l.KeyId,
            l.User?.Username ?? "unknown",
            l.AccessedAt,
            l.AccessMethod,
            l.IPAddress,
            l.IsSuccessful,
            l.FailureReason));
    }
}
