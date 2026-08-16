using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class KeyService : IKeyService
{
    private readonly SecureBoxDbContext _dbContext;
    private readonly IEncryptionService _encryption;
    private readonly IAuthService _auth;
    private readonly IAuditService _audit;
    private readonly ILogger<KeyService> _logger;

    public KeyService(
        SecureBoxDbContext dbContext,
        IEncryptionService encryption,
        IAuthService auth,
        IAuditService audit,
        ILogger<KeyService> logger)
    {
        _dbContext = dbContext;
        _encryption = encryption;
        _auth = auth;
        _audit = audit;
        _logger = logger;
    }

    public async Task<IEnumerable<KeyDto>> GetAllKeysAsync(KeyQueryParams queryParams, Guid userId, bool isAdmin)
    {
        var page = Math.Max(1, queryParams.Page);
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);
        var query = BuildQuery(queryParams, userId, isAdmin);

        var keys = await query
            .OrderByDescending(k => k.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return keys.Select(Map);
    }

    public async Task<KeyDto?> GetKeyByIdAsync(Guid keyId, Guid userId, bool isAdmin)
    {
        var key = await _dbContext.Keys
            .Include(k => k.Certificate)
            .Include(k => k.Owner)
            .FirstOrDefaultAsync(k => k.KeyId == keyId);

        if (key is null)
        {
            return null;
        }

        EnsureCanRead(key, userId, isAdmin);
        return Map(key);
    }

    public async Task<KeyDto> CreateKeyAsync(CreateKeyRequest request, Guid createdBy)
    {
        if (string.IsNullOrWhiteSpace(request.Value))
        {
            throw new InvalidOperationException("Key value is required");
        }

        if (request.Value.Length > 4096)
        {
            throw new InvalidOperationException("Key value cannot exceed 4KB");
        }

        if (!await _encryption.ValidateCertificateAsync(request.CertificateId))
        {
            throw new InvalidOperationException("Certificate is not valid for encryption");
        }

        var ownerId = request.OwnerUserId ?? createdBy;
        var (encrypted, iv, tag) = await _encryption.EncryptAsync(request.Value, request.CertificateId);

        var key = new Key
        {
            KeyId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description,
            KeyType = request.KeyType,
            EncryptedValue = encrypted,
            EncryptionIV = iv,
            EncryptionTag = tag,
            EncryptionAlgorithm = "AES256",
            CertificateId = request.CertificateId,
            EnvironmentTag = string.IsNullOrWhiteSpace(request.EnvironmentTag) ? "DEV" : request.EnvironmentTag,
            Tags = request.Tags is { Count: > 0 } ? JsonSerializer.Serialize(request.Tags) : null,
            Status = "Active",
            Version = 1,
            ValidFrom = request.ValidFrom ?? DateTime.UtcNow,
            ValidTo = request.ValidTo,
            ExpiresAt = request.ExpiresAt,
            OwnerUserId = ownerId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = createdBy,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Keys.Add(key);
        await _dbContext.SaveChangesAsync();

        await _audit.LogAuditTrailAsync(new AuditTrailDto(
            createdBy,
            "Key.Create",
            "Key",
            key.KeyId,
            $"{{\"name\":\"{key.Name}\",\"environment\":\"{key.EnvironmentTag}\"}}",
            null,
            null,
            "Info"));

        return Map(await LoadKeyAsync(key.KeyId));
    }

    public async Task<RetrieveKeyResponse> RetrieveKeyAsync(
        Guid keyId,
        Guid userId,
        string? reason,
        string? password,
        bool passwordRequired,
        string? ipAddress,
        string? userAgent,
        string accessMethod = "Portal")
    {
        var key = await LoadKeyAsync(keyId);
        var isAdmin = await IsAdminAsync(userId) || !passwordRequired;

        try
        {
            EnsureCanRead(key, userId, isAdmin);

            if (!string.Equals(key.Status, "Active", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Key cannot be retrieved because it is {key.Status}");
            }

            if (key.ExpiresAt.HasValue && key.ExpiresAt.Value <= DateTime.UtcNow)
            {
                key.Status = "Expired";
                await _dbContext.SaveChangesAsync();
                throw new InvalidOperationException("Key has expired");
            }

            if (passwordRequired)
            {
                if (string.IsNullOrWhiteSpace(password) || !await _auth.VerifyPasswordAsync(userId, password))
                {
                    throw new UnauthorizedAccessException("Password verification failed");
                }
            }

            var plaintext = await _encryption.DecryptAsync(
                key.EncryptedValue,
                key.EncryptionIV,
                key.EncryptionTag,
                key.CertificateId);

            key.LastAccessedAt = DateTime.UtcNow;
            key.AccessCount += 1;
            _dbContext.KeyAccessLogs.Add(new KeyAccessLog
            {
                AccessLogId = Guid.NewGuid(),
                KeyId = key.KeyId,
                AccessedBy = userId,
                AccessedAt = DateTime.UtcNow,
                AccessMethod = accessMethod,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccessful = true
            });
            await _dbContext.SaveChangesAsync();

            await _audit.LogAuditTrailAsync(new AuditTrailDto(
                userId,
                "Key.Retrieve",
                "Key",
                key.KeyId,
                string.IsNullOrWhiteSpace(reason) ? null : $"{{\"reason\":\"{reason}\"}}",
                ipAddress,
                userAgent,
                "Info"));

            return new RetrieveKeyResponse(key.KeyId, key.Name, plaintext, key.ExpiresAt, DateTime.UtcNow);
        }
        catch (Exception ex) when (ex is not KeyNotFoundException)
        {
            _dbContext.KeyAccessLogs.Add(new KeyAccessLog
            {
                AccessLogId = Guid.NewGuid(),
                KeyId = keyId,
                AccessedBy = userId,
                AccessedAt = DateTime.UtcNow,
                AccessMethod = accessMethod,
                IPAddress = ipAddress,
                UserAgent = userAgent,
                IsSuccessful = false,
                FailureReason = ex.Message
            });
            await _dbContext.SaveChangesAsync();
            throw;
        }
    }

    public async Task<KeyDto> UpdateKeyAsync(Guid keyId, UpdateKeyRequest request, Guid userId, bool isAdmin)
    {
        var key = await LoadKeyAsync(keyId);
        EnsureCanWrite(key, userId, isAdmin);

        if (request.Name != null)
        {
            key.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            key.Description = request.Description;
        }

        if (request.ExpiresAt.HasValue)
        {
            key.ExpiresAt = request.ExpiresAt;
        }

        key.UpdatedAt = DateTime.UtcNow;
        key.UpdatedBy = userId;
        await _dbContext.SaveChangesAsync();
        return Map(key);
    }

    public async Task<KeyDto> RotateKeyAsync(Guid keyId, string newValue, string? reason, Guid userId)
    {
        if (string.IsNullOrWhiteSpace(newValue))
        {
            throw new InvalidOperationException("New key value is required");
        }

        var existing = await LoadKeyAsync(keyId);
        var isAdmin = await IsAdminAsync(userId);
        EnsureCanWrite(existing, userId, isAdmin);

        if (!string.Equals(existing.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Only active keys can be rotated");
        }

        var (encrypted, iv, tag) = await _encryption.EncryptAsync(newValue, existing.CertificateId);

        existing.Status = "Archived";
        existing.UpdatedAt = DateTime.UtcNow;
        existing.UpdatedBy = userId;

        var rotated = new Key
        {
            KeyId = Guid.NewGuid(),
            Name = existing.Name,
            Description = existing.Description,
            KeyType = existing.KeyType,
            EncryptedValue = encrypted,
            EncryptionIV = iv,
            EncryptionTag = tag,
            EncryptionAlgorithm = existing.EncryptionAlgorithm,
            CertificateId = existing.CertificateId,
            EnvironmentTag = existing.EnvironmentTag,
            Tags = existing.Tags,
            Status = "Active",
            Version = existing.Version + 1,
            ValidFrom = DateTime.UtcNow,
            ValidTo = existing.ValidTo,
            ExpiresAt = existing.ExpiresAt,
            OwnerUserId = existing.OwnerUserId,
            CreatedAt = DateTime.UtcNow,
            CreatedBy = userId,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Keys.Add(rotated);
        await _dbContext.SaveChangesAsync();

        await _audit.LogAuditTrailAsync(new AuditTrailDto(
            userId,
            "Key.Rotate",
            "Key",
            rotated.KeyId,
            $"{{\"previousKeyId\":\"{existing.KeyId}\",\"reason\":\"{reason}\"}}",
            null,
            null,
            "Warning"));

        return Map(await LoadKeyAsync(rotated.KeyId));
    }

    public async Task RevokeKeyAsync(Guid keyId, string reason, Guid revokedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Revocation reason is required");
        }

        var key = await LoadKeyAsync(keyId);
        var isAdmin = await IsAdminAsync(revokedBy);
        EnsureCanWrite(key, revokedBy, isAdmin);

        key.Status = "Revoked";
        key.RevokedAt = DateTime.UtcNow;
        key.RevokedBy = revokedBy;
        key.RevokedReason = reason;
        key.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _audit.LogAuditTrailAsync(new AuditTrailDto(
            revokedBy,
            "Key.Revoke",
            "Key",
            keyId,
            $"{{\"reason\":\"{reason}\"}}",
            null,
            null,
            "Critical"));
    }

    public async Task DeleteKeyAsync(Guid keyId, Guid userId, bool isAdmin)
    {
        var key = await LoadKeyAsync(keyId);
        EnsureCanWrite(key, userId, isAdmin);

        if (string.Equals(key.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Active keys must be revoked before deletion");
        }

        key.DeletedAt = DateTime.UtcNow;
        key.UpdatedAt = DateTime.UtcNow;
        key.UpdatedBy = userId;
        await _dbContext.SaveChangesAsync();
    }

    private IQueryable<Key> BuildQuery(KeyQueryParams queryParams, Guid userId, bool isAdmin)
    {
        var query = _dbContext.Keys
            .Include(k => k.Certificate)
            .Include(k => k.Owner)
            .AsQueryable();

        if (!isAdmin)
        {
            query = query.Where(k => k.OwnerUserId == userId);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            query = query.Where(k => k.Status == queryParams.Status);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.KeyType))
        {
            query = query.Where(k => k.KeyType == queryParams.KeyType);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.EnvironmentTag))
        {
            query = query.Where(k => k.EnvironmentTag == queryParams.EnvironmentTag);
        }

        if (queryParams.CertificateId.HasValue)
        {
            query = query.Where(k => k.CertificateId == queryParams.CertificateId);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var term = queryParams.Search.Trim();
            query = query.Where(k => k.Name.Contains(term) || (k.Description != null && k.Description.Contains(term)));
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Tag))
        {
            var tag = queryParams.Tag;
            query = query.Where(k => k.Tags != null && k.Tags.Contains(tag));
        }

        if (queryParams.ExpiringIn30Days == true)
        {
            var until = DateTime.UtcNow.AddDays(30);
            query = query.Where(k => k.ExpiresAt != null && k.ExpiresAt <= until && k.Status == "Active");
        }

        return query;
    }

    private async Task<Key> LoadKeyAsync(Guid keyId)
    {
        return await _dbContext.Keys
                   .Include(k => k.Certificate)
                   .Include(k => k.Owner)
                   .FirstOrDefaultAsync(k => k.KeyId == keyId)
               ?? throw new KeyNotFoundException("Key not found");
    }

    private async Task<bool> IsAdminAsync(Guid userId)
    {
        return await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.Role.RoleName == "Admin");
    }

    private static void EnsureCanRead(Key key, Guid userId, bool isAdmin)
    {
        if (!isAdmin && key.OwnerUserId != userId)
        {
            throw new UnauthorizedAccessException("You do not have access to this key");
        }
    }

    private static void EnsureCanWrite(Key key, Guid userId, bool isAdmin) =>
        EnsureCanRead(key, userId, isAdmin);

    private static KeyDto Map(Key key)
    {
        List<string>? tags = null;
        if (!string.IsNullOrWhiteSpace(key.Tags))
        {
            try
            {
                tags = JsonSerializer.Deserialize<List<string>>(key.Tags);
            }
            catch (JsonException)
            {
                tags = null;
            }
        }

        return new KeyDto(
            key.KeyId,
            key.Name,
            key.Description,
            key.KeyType,
            key.EncryptionAlgorithm,
            key.EnvironmentTag,
            tags,
            key.Status,
            key.Version,
            key.ValidFrom,
            key.ValidTo,
            key.ExpiresAt,
            key.CertificateId,
            key.Certificate?.Name ?? string.Empty,
            key.Owner?.Username ?? "Unknown",
            key.CreatedAt,
            key.LastAccessedAt,
            key.AccessCount);
    }
}
