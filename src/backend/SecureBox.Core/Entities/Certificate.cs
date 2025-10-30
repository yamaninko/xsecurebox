namespace SecureBox.Core.Entities;

public class Certificate
{
    public Guid CertificateId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string Thumbprint { get; set; }
    public required string Subject { get; set; }
    public required string Issuer { get; set; }
    public required string SerialNumber { get; set; }
    public required string Algorithm { get; set; }
    public int KeySize { get; set; }
    public DateTime NotBefore { get; set; }
    public DateTime NotAfter { get; set; }
    public string Status { get; set; } = "Active"; // Active, Expired, Revoked, Pending
    public required string CertificateData { get; set; }
    public byte[]? PrivateKeyEncrypted { get; set; }
    public bool IsForSigning { get; set; } = false;
    public bool IsForEncryption { get; set; } = true;
    public Guid UploadedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public User? UploadedByUser { get; set; }
    public ICollection<Key> Keys { get; set; } = new List<Key>();
}

