namespace SecureBox.Core.Entities;

public class Key
{
    public Guid KeyId { get; set; }
    public required string Name { get; set; }
    public string? Description { get; set; }
    public required string KeyType { get; set; }
    public required byte[] EncryptedValue { get; set; }
    public required byte[] EncryptionIV { get; set; }
    public required byte[] EncryptionTag { get; set; }
    public Guid CertificateId { get; set; }
    public int Version { get; set; } = 1;
    public string Status { get; set; } = "Active"; // Active, Expired, Revoked, Archived
    public DateTime? ExpiresAt { get; set; }
    public Guid OwnerUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? RevokedAt { get; set; }
    public Guid? RevokedBy { get; set; }
    public string? RevokedReason { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime? LastAccessedAt { get; set; }
    public long AccessCount { get; set; } = 0;
    
    // Navigation properties
    public Certificate? Certificate { get; set; }
    public User? Owner { get; set; }
    public ICollection<KeyAccessLog> AccessLogs { get; set; } = new List<KeyAccessLog>();
}

