using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace SecureBox.Infrastructure.Migrations
{
    /// <summary>
    /// Initial database schema for Secure Box (Users, Roles, Permissions, Certificates, Keys, Logs, Audit).
    /// </summary>
    public partial class InitialCreate : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Users
            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    UserId = table.Column<Guid>(nullable: false),
                    Username = table.Column<string>(nullable: false),
                    Email = table.Column<string>(nullable: false),
                    PasswordHash = table.Column<string>(nullable: false),
                    FirstName = table.Column<string>(nullable: true),
                    LastName = table.Column<string>(nullable: true),
                    IsActive = table.Column<bool>(nullable: false),
                    IsEmailVerified = table.Column<bool>(nullable: false),
                    MustChangePassword = table.Column<bool>(nullable: false),
                    FailedLoginAttempts = table.Column<int>(nullable: false),
                    LastLoginAt = table.Column<DateTime>(nullable: true),
                    LockedOutUntil = table.Column<DateTime>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<Guid>(nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedBy = table.Column<Guid>(nullable: true),
                    DeletedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.UserId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Users_Username",
                table: "Users",
                column: "Username",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_Email",
                table: "Users",
                column: "Email",
                unique: true);

            // Roles
            migrationBuilder.CreateTable(
                name: "Roles",
                columns: table => new
                {
                    RoleId = table.Column<Guid>(nullable: false),
                    RoleName = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    IsSystem = table.Column<bool>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<Guid>(nullable: true),
                    UpdatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedBy = table.Column<Guid>(nullable: true),
                    DeletedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Roles", x => x.RoleId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Roles_RoleName",
                table: "Roles",
                column: "RoleName",
                unique: true);

            // Permissions
            migrationBuilder.CreateTable(
                name: "Permissions",
                columns: table => new
                {
                    PermissionId = table.Column<Guid>(nullable: false),
                    PermissionName = table.Column<string>(nullable: false),
                    Resource = table.Column<string>(nullable: false),
                    Action = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    CreatedAt = table.Column<DateTime>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Permissions", x => x.PermissionId);
                });

            // Certificates
            migrationBuilder.CreateTable(
                name: "Certificates",
                columns: table => new
                {
                    CertificateId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    Thumbprint = table.Column<string>(nullable: false),
                    Subject = table.Column<string>(nullable: false),
                    Issuer = table.Column<string>(nullable: false),
                    SerialNumber = table.Column<string>(nullable: false),
                    Algorithm = table.Column<string>(nullable: false),
                    KeySize = table.Column<int>(nullable: false),
                    NotBefore = table.Column<DateTime>(nullable: false),
                    NotAfter = table.Column<DateTime>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    CertificateData = table.Column<string>(nullable: false),
                    PrivateKeyEncrypted = table.Column<byte[]>(nullable: true),
                    IsForSigning = table.Column<bool>(nullable: false),
                    IsForEncryption = table.Column<bool>(nullable: false),
                    UploadedBy = table.Column<Guid>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false),
                    RevokedAt = table.Column<DateTime>(nullable: true),
                    RevokedBy = table.Column<Guid>(nullable: true),
                    RevokedReason = table.Column<string>(nullable: true),
                    DeletedAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Certificates", x => x.CertificateId);
                    table.ForeignKey(
                        name: "FK_Certificates_Users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_Thumbprint",
                table: "Certificates",
                column: "Thumbprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Certificates_UploadedBy",
                table: "Certificates",
                column: "UploadedBy");

            // Keys
            migrationBuilder.CreateTable(
                name: "Keys",
                columns: table => new
                {
                    KeyId = table.Column<Guid>(nullable: false),
                    Name = table.Column<string>(nullable: false),
                    Description = table.Column<string>(nullable: true),
                    KeyType = table.Column<string>(nullable: false),
                    EncryptedValue = table.Column<byte[]>(nullable: false),
                    EncryptionIV = table.Column<byte[]>(nullable: false),
                    EncryptionTag = table.Column<byte[]>(nullable: false),
                    CertificateId = table.Column<Guid>(nullable: false),
                    Version = table.Column<int>(nullable: false),
                    Status = table.Column<string>(nullable: false),
                    ExpiresAt = table.Column<DateTime>(nullable: true),
                    OwnerUserId = table.Column<Guid>(nullable: false),
                    CreatedAt = table.Column<DateTime>(nullable: false),
                    CreatedBy = table.Column<Guid>(nullable: false),
                    UpdatedAt = table.Column<DateTime>(nullable: false),
                    UpdatedBy = table.Column<Guid>(nullable: true),
                    RevokedAt = table.Column<DateTime>(nullable: true),
                    RevokedBy = table.Column<Guid>(nullable: true),
                    RevokedReason = table.Column<string>(nullable: true),
                    DeletedAt = table.Column<DateTime>(nullable: true),
                    LastAccessedAt = table.Column<DateTime>(nullable: true),
                    AccessCount = table.Column<long>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Keys", x => x.KeyId);
                    table.ForeignKey(
                        name: "FK_Keys_Certificates_CertificateId",
                        column: x => x.CertificateId,
                        principalTable: "Certificates",
                        principalColumn: "CertificateId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Keys_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Keys_CertificateId",
                table: "Keys",
                column: "CertificateId");

            migrationBuilder.CreateIndex(
                name: "IX_Keys_OwnerUserId",
                table: "Keys",
                column: "OwnerUserId");

            // AuditTrail
            migrationBuilder.CreateTable(
                name: "AuditTrails",
                columns: table => new
                {
                    AuditId = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: true),
                    Action = table.Column<string>(nullable: false),
                    Resource = table.Column<string>(nullable: false),
                    ResourceId = table.Column<Guid>(nullable: true),
                    Details = table.Column<string>(nullable: true),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    Timestamp = table.Column<DateTime>(nullable: false),
                    Severity = table.Column<string>(nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditTrails", x => x.AuditId);
                    table.ForeignKey(
                        name: "FK_AuditTrails_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_Timestamp",
                table: "AuditTrails",
                column: "Timestamp");

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_Resource_ResourceId",
                table: "AuditTrails",
                columns: new[] { "Resource", "ResourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_AuditTrails_UserId",
                table: "AuditTrails",
                column: "UserId");

            // KeyAccessLogs
            migrationBuilder.CreateTable(
                name: "KeyAccessLogs",
                columns: table => new
                {
                    AccessLogId = table.Column<Guid>(nullable: false),
                    KeyId = table.Column<Guid>(nullable: false),
                    AccessedBy = table.Column<Guid>(nullable: false),
                    AccessedAt = table.Column<DateTime>(nullable: false),
                    AccessMethod = table.Column<string>(nullable: false),
                    IPAddress = table.Column<string>(nullable: true),
                    UserAgent = table.Column<string>(nullable: true),
                    IsSuccessful = table.Column<bool>(nullable: false),
                    FailureReason = table.Column<string>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyAccessLogs", x => x.AccessLogId);
                    table.ForeignKey(
                        name: "FK_KeyAccessLogs_Keys_KeyId",
                        column: x => x.KeyId,
                        principalTable: "Keys",
                        principalColumn: "KeyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_KeyAccessLogs_Users_AccessedBy",
                        column: x => x.AccessedBy,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeyAccessLogs_KeyId",
                table: "KeyAccessLogs",
                column: "KeyId");

            migrationBuilder.CreateIndex(
                name: "IX_KeyAccessLogs_AccessedBy",
                table: "KeyAccessLogs",
                column: "AccessedBy");

            migrationBuilder.CreateIndex(
                name: "IX_KeyAccessLogs_AccessedAt",
                table: "KeyAccessLogs",
                column: "AccessedAt");

            // UserRoles
            migrationBuilder.CreateTable(
                name: "UserRoles",
                columns: table => new
                {
                    UserRoleId = table.Column<Guid>(nullable: false),
                    UserId = table.Column<Guid>(nullable: false),
                    RoleId = table.Column<Guid>(nullable: false),
                    AssignedAt = table.Column<DateTime>(nullable: false),
                    AssignedBy = table.Column<Guid>(nullable: true),
                    ExpiresAt = table.Column<DateTime>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserRoles", x => x.UserRoleId);
                    table.ForeignKey(
                        name: "FK_UserRoles_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_UserRoles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "UserId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId_RoleId",
                table: "UserRoles",
                columns: new[] { "UserId", "RoleId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_RoleId",
                table: "UserRoles",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_UserRoles_UserId",
                table: "UserRoles",
                column: "UserId");

            // RolePermissions
            migrationBuilder.CreateTable(
                name: "RolePermissions",
                columns: table => new
                {
                    RolePermissionId = table.Column<Guid>(nullable: false),
                    RoleId = table.Column<Guid>(nullable: false),
                    PermissionId = table.Column<Guid>(nullable: false),
                    GrantedAt = table.Column<DateTime>(nullable: false),
                    GrantedBy = table.Column<Guid>(nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RolePermissions", x => x.RolePermissionId);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "Permissions",
                        principalColumn: "PermissionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RolePermissions_Roles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "Roles",
                        principalColumn: "RoleId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId_PermissionId",
                table: "RolePermissions",
                columns: new[] { "RoleId", "PermissionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_PermissionId",
                table: "RolePermissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_RolePermissions_RoleId",
                table: "RolePermissions",
                column: "RoleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "KeyAccessLogs");
            migrationBuilder.DropTable(name: "AuditTrails");
            migrationBuilder.DropTable(name: "RolePermissions");
            migrationBuilder.DropTable(name: "UserRoles");
            migrationBuilder.DropTable(name: "Keys");
            migrationBuilder.DropTable(name: "Permissions");
            migrationBuilder.DropTable(name: "Roles");
            migrationBuilder.DropTable(name: "Certificates");
            migrationBuilder.DropTable(name: "Users");
        }
    }
}

