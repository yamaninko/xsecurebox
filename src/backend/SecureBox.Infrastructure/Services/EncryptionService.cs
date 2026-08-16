using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class EncryptionService : IEncryptionService
{
    public const string DevKek = "SecureBox-Dev-KEK-32-bytes!!!!!!";

    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<EncryptionService> _logger;
    private readonly byte[] _kek;

    public EncryptionService(
        SecureBoxDbContext dbContext,
        IConfiguration configuration,
        ILogger<EncryptionService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
        _kek = ResolveKek(configuration);
    }

    public async Task<(byte[] encrypted, byte[] iv, byte[] tag)> EncryptAsync(string plaintext, Guid certificateId)
    {
        ArgumentException.ThrowIfNullOrEmpty(plaintext);

        var certificate = await LoadActiveCertificateAsync(certificateId);
        using var publicCert = LoadPublicCertificate(certificate.CertificateData);

        var dek = RandomNumberGenerator.GetBytes(32);
        var iv = RandomNumberGenerator.GetBytes(12);
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertext = new byte[plaintextBytes.Length];
        var tag = new byte[16];

        using (var aes = new AesGcm(dek, 16))
        {
            aes.Encrypt(iv, plaintextBytes, ciphertext, tag);
        }

        var wrappedDek = publicCert.GetRSAPublicKey()?.Encrypt(dek, RSAEncryptionPadding.OaepSHA256)
                         ?? throw new InvalidOperationException("Certificate does not contain an RSA public key");

        CryptographicOperations.ZeroMemory(dek);

        var packed = new byte[4 + wrappedDek.Length + ciphertext.Length];
        BitConverter.TryWriteBytes(packed.AsSpan(0, 4), wrappedDek.Length);
        Buffer.BlockCopy(wrappedDek, 0, packed, 4, wrappedDek.Length);
        Buffer.BlockCopy(ciphertext, 0, packed, 4 + wrappedDek.Length, ciphertext.Length);

        return (packed, iv, tag);
    }

    public async Task<string> DecryptAsync(byte[] ciphertext, byte[] iv, byte[] tag, Guid certificateId)
    {
        var certificate = await LoadActiveCertificateAsync(certificateId);
        if (certificate.PrivateKeyEncrypted is null || certificate.PrivateKeyEncrypted.Length == 0)
        {
            throw new InvalidOperationException("Certificate private key is not stored; cannot decrypt");
        }

        if (ciphertext.Length < 5)
        {
            throw new InvalidOperationException("Encrypted payload is corrupt");
        }

        var wrappedLength = BitConverter.ToInt32(ciphertext, 0);
        if (wrappedLength <= 0 || wrappedLength > ciphertext.Length - 4)
        {
            throw new InvalidOperationException("Encrypted payload is corrupt");
        }

        var wrappedDek = ciphertext.AsSpan(4, wrappedLength).ToArray();
        var aesCiphertext = ciphertext.AsSpan(4 + wrappedLength).ToArray();

        using var rsa = RSA.Create();
        rsa.ImportPkcs8PrivateKey(UnprotectPrivateKey(certificate.PrivateKeyEncrypted), out _);

        var dek = rsa.Decrypt(wrappedDek, RSAEncryptionPadding.OaepSHA256);
        var plaintextBytes = new byte[aesCiphertext.Length];

        try
        {
            using var aes = new AesGcm(dek, 16);
            aes.Decrypt(iv, aesCiphertext, tag, plaintextBytes);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(dek);
        }

        return Encoding.UTF8.GetString(plaintextBytes);
    }

    public async Task<bool> ValidateCertificateAsync(Guid certificateId)
    {
        try
        {
            var certificate = await LoadActiveCertificateAsync(certificateId);
            return certificate.PrivateKeyEncrypted is { Length: > 0 };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Certificate {CertificateId} failed validation", certificateId);
            return false;
        }
    }

    public byte[] ProtectPrivateKey(byte[] pkcs8PrivateKey)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var tag = new byte[16];
        var ciphertext = new byte[pkcs8PrivateKey.Length];

        using (var aes = new AesGcm(_kek, 16))
        {
            aes.Encrypt(iv, pkcs8PrivateKey, ciphertext, tag);
        }

        var packed = new byte[12 + 16 + ciphertext.Length];
        Buffer.BlockCopy(iv, 0, packed, 0, 12);
        Buffer.BlockCopy(tag, 0, packed, 12, 16);
        Buffer.BlockCopy(ciphertext, 0, packed, 28, ciphertext.Length);
        return packed;
    }

    public byte[] UnprotectPrivateKey(byte[] protectedKey)
    {
        if (protectedKey.Length < 29)
        {
            throw new InvalidOperationException("Stored private key is corrupt");
        }

        var iv = protectedKey.AsSpan(0, 12);
        var tag = protectedKey.AsSpan(12, 16);
        var ciphertext = protectedKey.AsSpan(28);
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(_kek, 16);
        aes.Decrypt(iv, ciphertext, tag, plaintext);
        return plaintext;
    }

    private async Task<Core.Entities.Certificate> LoadActiveCertificateAsync(Guid certificateId)
    {
        var certificate = await _dbContext.Certificates
            .FirstOrDefaultAsync(c => c.CertificateId == certificateId);

        if (certificate is null)
        {
            throw new KeyNotFoundException("Certificate not found");
        }

        if (!string.Equals(certificate.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Certificate is {certificate.Status}");
        }

        if (certificate.NotAfter <= DateTime.UtcNow)
        {
            certificate.Status = "Expired";
            await _dbContext.SaveChangesAsync();
            throw new InvalidOperationException("Certificate has expired");
        }

        if (!certificate.IsForEncryption)
        {
            throw new InvalidOperationException("Certificate is not enabled for encryption");
        }

        return certificate;
    }

    internal static X509Certificate2 LoadPublicCertificate(string pemOrBase64)
    {
        var data = pemOrBase64.Trim();
        if (data.Contains("BEGIN CERTIFICATE", StringComparison.OrdinalIgnoreCase))
        {
            return X509Certificate2.CreateFromPem(data);
        }

        return X509CertificateLoader.LoadCertificate(Convert.FromBase64String(data));
    }

    internal static byte[] ResolveKek(IConfiguration configuration)
    {
        var configured = configuration["Encryption:KeyEncryptionKey"];
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException("Encryption:KeyEncryptionKey is not configured");
        }

        if (configured.Length == 32)
        {
            return Encoding.UTF8.GetBytes(configured);
        }

        try
        {
            var decoded = Convert.FromBase64String(configured);
            if (decoded.Length == 32)
            {
                return decoded;
            }
        }
        catch (FormatException)
        {
            // fall through
        }

        throw new InvalidOperationException("Encryption:KeyEncryptionKey must be 32 UTF-8 bytes or 32-byte base64");
    }
}
