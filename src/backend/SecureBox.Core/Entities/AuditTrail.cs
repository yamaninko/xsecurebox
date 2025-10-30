namespace SecureBox.Core.Entities;

public class AuditTrail
{
    public Guid AuditId { get; set; }
    public Guid? UserId { get; set; }
    public required string Action { get; set; }
    public required string Resource { get; set; }
    public Guid? ResourceId { get; set; }
    public string? Details { get; set; } // JSON string
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    public string Severity { get; set; } = "Info"; // Info, Warning, Critical
    
    public User? User { get; set; }
}

public class KeyAccessLog
{
    public Guid AccessLogId { get; set; }
    public Guid KeyId { get; set; }
    public Guid AccessedBy { get; set; }
    public DateTime AccessedAt { get; set; } = DateTime.UtcNow;
    public required string AccessMethod { get; set; }
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public bool IsSuccessful { get; set; }
    public string? FailureReason { get; set; }
    
    public Key? Key { get; set; }
    public User? User { get; set; }
}

