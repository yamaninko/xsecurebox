using Microsoft.EntityFrameworkCore;
using SecureBox.Core.Entities;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Tests;

public class DbContextModelTests
{
    private static SecureBoxDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<SecureBoxDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SecureBoxDbContext(options);
    }

    [Fact]
    public void Model_Has_Unique_Indexes_On_User_Username_And_Email()
    {
        using var db = CreateInMemoryDb();
        var userEntity = db.Model.FindEntityType(typeof(User));
        userEntity.Should().NotBeNull();

        var indexes = userEntity!.GetIndexes();
        indexes.Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(User.Username)))
            .Should().BeTrue();
        indexes.Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(User.Email)))
            .Should().BeTrue();
    }

    [Fact]
    public void Model_Has_Unique_Index_On_Role_RoleName()
    {
        using var db = CreateInMemoryDb();
        var roleEntity = db.Model.FindEntityType(typeof(Role));
        roleEntity.Should().NotBeNull();

        var indexes = roleEntity!.GetIndexes();
        indexes.Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Role.RoleName)))
            .Should().BeTrue();
    }

    [Fact]
    public void Model_Has_Unique_Index_On_Certificate_Thumbprint_And_Relations()
    {
        using var db = CreateInMemoryDb();
        var certEntity = db.Model.FindEntityType(typeof(Certificate));
        certEntity.Should().NotBeNull();

        var indexes = certEntity!.GetIndexes();
        indexes.Any(i => i.IsUnique && i.Properties.Any(p => p.Name == nameof(Certificate.Thumbprint)))
            .Should().BeTrue();

        var fks = certEntity.GetForeignKeys();
        fks.Any(fk => fk.Properties.Any(p => p.Name == nameof(Certificate.UploadedBy)))
            .Should().BeTrue();
    }

    [Fact]
    public void Model_Key_Has_Restrict_Delete_To_Certificate_And_Owner()
    {
        using var db = CreateInMemoryDb();
        var entity = db.Model.FindEntityType(typeof(Key));
        entity.Should().NotBeNull();
        var fks = entity!.GetForeignKeys().ToList();

        var fkCert = fks.FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(Key.CertificateId)));
        fkCert.Should().NotBeNull();
        fkCert!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var fkOwner = fks.FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(Key.OwnerUserId)));
        fkOwner.Should().NotBeNull();
        fkOwner!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void Model_AuditTrail_Has_Indexes_On_Timestamp_And_Resource()
    {
        using var db = CreateInMemoryDb();
        var entity = db.Model.FindEntityType(typeof(AuditTrail));
        entity.Should().NotBeNull();
        var indexes = entity!.GetIndexes();
        indexes.Any(i => i.Properties.Any(p => p.Name == nameof(AuditTrail.Timestamp)))
            .Should().BeTrue();
        indexes.Any(i => i.Properties.Select(p => p.Name).OrderBy(n => n)
            .SequenceEqual(new[] { nameof(AuditTrail.Resource), nameof(AuditTrail.ResourceId) }.OrderBy(n => n)))
            .Should().BeTrue();
    }

    [Fact]
    public void Model_KeyAccessLog_Relations_And_DeleteBehavior_Configured()
    {
        using var db = CreateInMemoryDb();
        var logEntity = db.Model.FindEntityType(typeof(KeyAccessLog));
        logEntity.Should().NotBeNull();

        var fks = logEntity!.GetForeignKeys().ToList();

        var fkKey = fks.FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(KeyAccessLog.KeyId)));
        fkKey.Should().NotBeNull();
        fkKey!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);

        var fkUser = fks.FirstOrDefault(fk => fk.Properties.Any(p => p.Name == nameof(KeyAccessLog.AccessedBy)));
        fkUser.Should().NotBeNull();
        fkUser!.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        var indexes = logEntity.GetIndexes();
        indexes.Any(i => i.Properties.Any(p => p.Name == nameof(KeyAccessLog.AccessedAt)))
            .Should().BeTrue();
    }

    [Fact]
    public async Task QueryFilter_Excludes_SoftDeleted_Users()
    {
        using var db = CreateInMemoryDb();
        db.Users.Add(new User { UserId = Guid.NewGuid(), Username = "active", Email = "a@a.com", PasswordHash = "x" });
        db.Users.Add(new User { UserId = Guid.NewGuid(), Username = "deleted", Email = "d@a.com", PasswordHash = "x", DeletedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();

        var count = await db.Users.CountAsync();
        count.Should().Be(1);
        (await db.Users.AnyAsync(u => u.Username == "deleted")).Should().BeFalse();
    }

    [Fact]
    public void UserRole_Has_Unique_UserId_RoleId_Index()
    {
        using var db = CreateInMemoryDb();
        var entity = db.Model.FindEntityType(typeof(UserRole));
        entity.Should().NotBeNull();
        var idx = entity!.GetIndexes();
        idx.Any(i => i.IsUnique && i.Properties.Select(p => p.Name).OrderBy(n => n)
            .SequenceEqual(new[] { nameof(UserRole.RoleId), nameof(UserRole.UserId) }.OrderBy(n => n)))
            .Should().BeTrue();
    }
}
