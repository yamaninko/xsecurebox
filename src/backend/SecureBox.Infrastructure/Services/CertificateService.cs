using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class CertificateService : ICertificateService
{
    private const int MaxCertificateBytes = 10 * 1024 * 1024;
    private readonly SecureBoxDbContext _dbContext;
    private readonly EncryptionService _encryption;
    private readonly IAuditService _audit;
    private readonly ILogger<CertificateService> _logger;

    public CertificateService(
        SecureBoxDbContext dbContext,
        IEncryptionService encryption,
        IAuditService audit,
        ILogger<CertificateService> logger)
    {
        _dbContext = dbContext;
        _encryption = encryption as EncryptionService
                      ?? throw new InvalidOperationException("EncryptionService implementation required");
        _audit = audit;
        _logger = logger;
    }

    public async Task<IEnumerable<CertificateDto>> GetAllCertificatesAsync(CertificateQueryParams queryParams)
    {
        var page = Math.Max(1, queryParams.Page);
        var pageSize = Math.Clamp(queryParams.PageSize, 1, 100);

        var query = _dbContext.Certificates.AsNoTracking().Include(c => c.UploadedByUser).AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.Status))
        {
            query = query.Where(c => c.Status == queryParams.Status);
        }

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            var term = queryParams.Search.Trim();
            query = query.Where(c =>
                c.Name.Contains(term) ||
                c.Subject.Contains(term) ||
                c.Thumbprint.Contains(term));
        }

        var certificates = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return certificates.Select(Map);
    }

    public async Task<CertificateDto?> GetCertificateByIdAsync(Guid certificateId)
    {
        var certificate = await _dbContext.Certificates
            .AsNoTracking()
            .Include(c => c.UploadedByUser)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

        return certificate is null ? null : Map(certificate);
    }

    public async Task<CertificateDto> UploadCertificateAsync(UploadCertificateRequest request, Guid uploadedBy)
    {
        if (request.CertificateFile is null || request.CertificateFile.Length == 0)
        {
            throw new InvalidOperationException("Certificate file is required");
        }

        if (request.CertificateFile.Length > MaxCertificateBytes)
        {
            throw new InvalidOperationException("Certificate file exceeds 10MB");
        }

        using var parsed = ParseCertificate(request.CertificateFile, request.Password);
        if (parsed.NotAfter <= DateTime.UtcNow)
        {
            throw new InvalidOperationException("Certificate has expired");
        }

        var rsa = parsed.GetRSAPublicKey();
        if (rsa is null)
        {
            throw new InvalidOperationException("Only RSA certificates are supported");
        }

        var keySize = rsa.KeySize;
        if (keySize < 2048)
        {
            throw new InvalidOperationException("Certificate key size must be at least 2048 bits");
        }

        var hasPrivateKey = parsed.HasPrivateKey;
        if (request.IsForEncryption && !hasPrivateKey)
        {
            throw new InvalidOperationException("Encryption certificates must include a private key (PFX/P12)");
        }

        var thumbprint = parsed.Thumbprint;
        if (await _dbContext.Certificates.AnyAsync(c => c.Thumbprint == thumbprint))
        {
            throw new InvalidOperationException("A certificate with this thumbprint already exists");
        }

        byte[]? protectedPrivateKey = null;
        if (hasPrivateKey)
        {
            using var privateRsa = parsed.GetRSAPrivateKey()
                                   ?? throw new InvalidOperationException("Unable to export certificate private key");
            protectedPrivateKey = _encryption.ProtectPrivateKey(privateRsa.ExportPkcs8PrivateKey());
        }

        var entity = new Certificate
        {
            CertificateId = Guid.NewGuid(),
            Name = request.Name.Trim(),
            Description = request.Description,
            Thumbprint = thumbprint,
            Subject = parsed.Subject,
            Issuer = parsed.Issuer,
            SerialNumber = parsed.SerialNumber,
            Algorithm = parsed.SignatureAlgorithm.FriendlyName ?? parsed.SignatureAlgorithm.Value ?? "RSA",
            KeySize = keySize,
            NotBefore = parsed.NotBefore.ToUniversalTime(),
            NotAfter = parsed.NotAfter.ToUniversalTime(),
            Status = "Active",
            CertificateData = ExportPublicPem(parsed),
            PrivateKeyEncrypted = protectedPrivateKey,
            IsForSigning = request.IsForSigning,
            IsForEncryption = request.IsForEncryption && hasPrivateKey,
            UploadedBy = uploadedBy,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Certificates.Add(entity);
        await _dbContext.SaveChangesAsync();

        await _audit.LogAuditTrailAsync(new AuditTrailDto(
            uploadedBy,
            "Certificate.Upload",
            "Certificate",
            entity.CertificateId,
            $"{{\"name\":\"{entity.Name}\"}}",
            null,
            null,
            "Info"));

        _logger.LogInformation("Certificate {CertificateId} uploaded by {UserId}", entity.CertificateId, uploadedBy);

        var uploaded = await _dbContext.Certificates
            .Include(c => c.UploadedByUser)
            .FirstAsync(c => c.CertificateId == entity.CertificateId);

        return Map(uploaded);
    }

    public async Task<CertificateDto> UpdateCertificateAsync(Guid certificateId, UpdateCertificateRequest request)
    {
        var certificate = await _dbContext.Certificates
            .Include(c => c.UploadedByUser)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId)
            ?? throw new KeyNotFoundException("Certificate not found");

        if (request.Name != null)
        {
            certificate.Name = request.Name.Trim();
        }

        if (request.Description != null)
        {
            certificate.Description = request.Description;
        }

        certificate.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return Map(certificate);
    }

    public async Task RevokeCertificateAsync(Guid certificateId, string reason, Guid revokedBy)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new InvalidOperationException("Revocation reason is required");
        }

        var certificate = await _dbContext.Certificates
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId)
            ?? throw new KeyNotFoundException("Certificate not found");

        certificate.Status = "Revoked";
        certificate.RevokedAt = DateTime.UtcNow;
        certificate.RevokedBy = revokedBy;
        certificate.RevokedReason = reason;
        certificate.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _audit.LogAuditTrailAsync(new AuditTrailDto(
            revokedBy,
            "Certificate.Revoke",
            "Certificate",
            certificateId,
            $"{{\"reason\":\"{reason}\"}}",
            null,
            null,
            "Critical"));
    }

    public async Task DeleteCertificateAsync(Guid certificateId)
    {
        var certificate = await _dbContext.Certificates
            .Include(c => c.Keys)
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId)
            ?? throw new KeyNotFoundException("Certificate not found");

        if (certificate.Keys.Any(k => k.Status == "Active" && k.DeletedAt == null))
        {
            throw new InvalidOperationException("Cannot delete a certificate that still has active keys");
        }

        certificate.DeletedAt = DateTime.UtcNow;
        certificate.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    private static CertificateDto Map(Certificate certificate) =>
        new(
            certificate.CertificateId,
            certificate.Name,
            certificate.Description,
            certificate.Thumbprint,
            certificate.Subject,
            certificate.Issuer,
            certificate.SerialNumber,
            certificate.Algorithm,
            certificate.KeySize,
            certificate.NotBefore,
            certificate.NotAfter,
            certificate.Status,
            certificate.IsForSigning,
            certificate.IsForEncryption,
            certificate.UploadedByUser?.Username ?? "Unknown",
            certificate.CreatedAt);

    private static X509Certificate2 ParseCertificate(byte[] file, string? password)
    {
        try
        {
            var text = System.Text.Encoding.UTF8.GetString(file);
            if (text.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
            {
                if (text.Contains("BEGIN", StringComparison.OrdinalIgnoreCase) &&
                    (text.Contains("PRIVATE KEY", StringComparison.OrdinalIgnoreCase)))
                {
                    return X509Certificate2.CreateFromPem(text, text);
                }

                return X509Certificate2.CreateFromPem(text);
            }

            return X509CertificateLoader.LoadPkcs12(
                file,
                password,
                X509KeyStorageFlags.Exportable | X509KeyStorageFlags.EphemeralKeySet);
        }
        catch (Exception ex) when (ex is CryptographicException or ArgumentException)
        {
            throw new InvalidOperationException("Invalid certificate file or password", ex);
        }
    }

    private static string ExportPublicPem(X509Certificate2 certificate)
    {
        var builder = new System.Text.StringBuilder();
        builder.AppendLine("-----BEGIN CERTIFICATE-----");
        builder.AppendLine(Convert.ToBase64String(certificate.Export(X509ContentType.Cert), Base64FormattingOptions.InsertLineBreaks));
        builder.AppendLine("-----END CERTIFICATE-----");
        return builder.ToString();
    }
}
