using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public interface ILifecycleService
{
    Task<ExpirySweepResult> SweepAsync(CancellationToken cancellationToken = default);
}

public sealed record ExpirySweepResult(int ExpiredKeys, int ExpiredCertificates);

public class LifecycleService : ILifecycleService
{
    private readonly SecureBoxDbContext _dbContext;
    private readonly IAuditService _audit;
    private readonly ILogger<LifecycleService> _logger;

    public LifecycleService(SecureBoxDbContext dbContext, IAuditService audit, ILogger<LifecycleService> logger)
    {
        _dbContext = dbContext;
        _audit = audit;
        _logger = logger;
    }

    public async Task<ExpirySweepResult> SweepAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var expiredKeys = await _dbContext.Keys
            .Where(k => k.Status == "Active" && k.ExpiresAt != null && k.ExpiresAt <= now)
            .ToListAsync(cancellationToken);

        foreach (var key in expiredKeys)
        {
            key.Status = "Expired";
            key.UpdatedAt = now;
        }

        var expiredCerts = await _dbContext.Certificates
            .Where(c => c.Status == "Active" && c.NotAfter <= now)
            .ToListAsync(cancellationToken);

        foreach (var cert in expiredCerts)
        {
            cert.Status = "Expired";
            cert.UpdatedAt = now;
        }

        if (expiredKeys.Count > 0 || expiredCerts.Count > 0)
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
            await _audit.LogAuditTrailAsync(new AuditTrailDto(
                null,
                "Lifecycle.Sweep",
                "System",
                null,
                $"{{\"expiredKeys\":{expiredKeys.Count},\"expiredCertificates\":{expiredCerts.Count}}}",
                null,
                null,
                "Warning"));
        }

        _logger.LogInformation(
            "Expiry sweep marked {KeyCount} keys and {CertCount} certificates expired",
            expiredKeys.Count,
            expiredCerts.Count);

        return new ExpirySweepResult(expiredKeys.Count, expiredCerts.Count);
    }
}
