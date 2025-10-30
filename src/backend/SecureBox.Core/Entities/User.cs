namespace SecureBox.Core.Entities;

public class User
{
    public Guid UserId { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsEmailVerified { get; set; } = false;
    public bool MustChangePassword { get; set; } = false;
    public int FailedLoginAttempts { get; set; } = 0;
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LockedOutUntil { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedBy { get; set; }
    public DateTime? DeletedAt { get; set; }
    
    // Navigation properties
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
    public ICollection<Certificate> Certificates { get; set; } = new List<Certificate>();
    public ICollection<Key> OwnedKeys { get; set; } = new List<Key>();
}

