using Microsoft.EntityFrameworkCore;
using SecureBox.Core.Entities;

namespace SecureBox.Infrastructure.Data;

public class SecureBoxDbContext : DbContext
{
    public SecureBoxDbContext(DbContextOptions<SecureBoxDbContext> options) : base(options)
    {
    }
    
    public DbSet<User> Users { get; set; }
    public DbSet<Role> Roles { get; set; }
    public DbSet<Permission> Permissions { get; set; }
    public DbSet<UserRole> UserRoles { get; set; }
    public DbSet<RolePermission> RolePermissions { get; set; }
    public DbSet<Certificate> Certificates { get; set; }
    public DbSet<Key> Keys { get; set; }
    public DbSet<KeyAccessLog> KeyAccessLogs { get; set; }
    public DbSet<AuditTrail> AuditTrails { get; set; }
    public DbSet<ApiClient> ApiClients { get; set; }
    public DbSet<ApiClientRequest> ApiClientRequests { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
        
        // Role configuration
        modelBuilder.Entity<Role>(entity =>
        {
            entity.HasKey(e => e.RoleId);
            entity.HasIndex(e => e.RoleName).IsUnique();
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
        
        // Certificate configuration
        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.HasKey(e => e.CertificateId);
            entity.HasIndex(e => e.Thumbprint).IsUnique();
            entity.HasOne(e => e.UploadedByUser)
                  .WithMany(e => e.Certificates)
                  .HasForeignKey(e => e.UploadedBy)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
        
        // Key configuration
        modelBuilder.Entity<Key>(entity =>
        {
            entity.HasKey(e => e.KeyId);
            entity.HasOne(e => e.Certificate)
                  .WithMany(e => e.Keys)
                  .HasForeignKey(e => e.CertificateId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Owner)
                  .WithMany(e => e.OwnedKeys)
                  .HasForeignKey(e => e.OwnerUserId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
        
        // KeyAccessLog configuration
        modelBuilder.Entity<KeyAccessLog>(entity =>
        {
            entity.HasKey(e => e.AccessLogId);
            entity.HasOne(e => e.Key)
                  .WithMany(e => e.AccessLogs)
                  .HasForeignKey(e => e.KeyId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.AccessedBy)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => e.AccessedAt);
            // Match the query filter on Key entity to avoid issues with filtered required relationships
            entity.HasQueryFilter(e => e.Key.DeletedAt == null);
        });
        
        // AuditTrail configuration
        modelBuilder.Entity<AuditTrail>(entity =>
        {
            entity.HasKey(e => e.AuditId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => new { e.Resource, e.ResourceId });
        });
        
        // UserRole configuration
        modelBuilder.Entity<UserRole>(entity =>
        {
            entity.HasKey(e => e.UserRoleId);
            entity.HasIndex(e => new { e.UserId, e.RoleId }).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany(e => e.UserRoles)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Role)
                  .WithMany(e => e.UserRoles)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Match the query filter on Role entity to avoid issues with filtered required relationships
            entity.HasQueryFilter(e => e.Role.DeletedAt == null);
        });
        
        // RolePermission configuration
        modelBuilder.Entity<RolePermission>(entity =>
        {
            entity.HasKey(e => e.RolePermissionId);
            entity.HasIndex(e => new { e.RoleId, e.PermissionId }).IsUnique();
            entity.HasOne(e => e.Role)
                  .WithMany(e => e.RolePermissions)
                  .HasForeignKey(e => e.RoleId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Permission)
                  .WithMany(e => e.RolePermissions)
                  .HasForeignKey(e => e.PermissionId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Match the query filter on Role entity to avoid issues with filtered required relationships
            entity.HasQueryFilter(e => e.Role.DeletedAt == null);
        });
        
        // ApiClient configuration
        modelBuilder.Entity<ApiClient>(entity =>
        {
            entity.HasKey(e => e.ClientId);
            entity.HasIndex(e => e.ClientIdString).IsUnique();
            entity.HasIndex(e => e.ApiKey).IsUnique();
            entity.HasOne(e => e.Creator)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedBy)
                  .OnDelete(DeleteBehavior.Restrict);
        });
        
        // ApiClientRequest configuration
        modelBuilder.Entity<ApiClientRequest>(entity =>
        {
            entity.HasKey(e => e.RequestId);
            entity.HasIndex(e => e.RequestedAt);
            entity.HasIndex(e => e.ClientId);
            entity.HasOne(e => e.Client)
                  .WithMany(e => e.Requests)
                  .HasForeignKey(e => e.ClientId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
