-- Secure Box Database Schema
-- PostgreSQL 16+ required

-- Enable required extensions
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS "pgcrypto";

-- Database: secureboxdb (already created by POSTGRES_DB env var)

-- ============================================
-- TABLES
-- ============================================

-- Users Table
CREATE TABLE IF NOT EXISTS "Users" (
    "UserId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Username" VARCHAR(100) NOT NULL UNIQUE,
    "Email" VARCHAR(255) NOT NULL UNIQUE,
    "PasswordHash" VARCHAR(512) NOT NULL,
    "FirstName" VARCHAR(100),
    "LastName" VARCHAR(100),
    "IsActive" BOOLEAN DEFAULT TRUE NOT NULL,
    "IsEmailVerified" BOOLEAN DEFAULT FALSE NOT NULL,
    "MustChangePassword" BOOLEAN DEFAULT FALSE NOT NULL,
    "FailedLoginAttempts" INT DEFAULT 0 NOT NULL,
    "LastLoginAt" TIMESTAMP WITH TIME ZONE,
    "LockedOutUntil" TIMESTAMP WITH TIME ZONE,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "CreatedBy" UUID,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "UpdatedBy" UUID,
    "DeletedAt" TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT "chk_email_format" CHECK ("Email" ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'),
    CONSTRAINT "chk_failed_login_non_negative" CHECK ("FailedLoginAttempts" >= 0)
);

CREATE INDEX "idx_users_username" ON "Users"("Username") WHERE "DeletedAt" IS NULL;
CREATE INDEX "idx_users_email" ON "Users"("Email") WHERE "DeletedAt" IS NULL;
CREATE INDEX "idx_users_isactive" ON "Users"("IsActive") WHERE "DeletedAt" IS NULL;

-- Roles Table
CREATE TABLE IF NOT EXISTS "Roles" (
    "RoleId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "RoleName" VARCHAR(50) NOT NULL UNIQUE,
    "Description" VARCHAR(500),
    "IsSystem" BOOLEAN DEFAULT FALSE NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "CreatedBy" UUID,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "UpdatedBy" UUID,
    "DeletedAt" TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT "chk_rolename_not_empty" CHECK (TRIM("RoleName") <> '')
);

CREATE INDEX "idx_roles_rolename" ON "Roles"("RoleName") WHERE "DeletedAt" IS NULL;

-- Permissions Table
CREATE TABLE IF NOT EXISTS "Permissions" (
    "PermissionId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PermissionName" VARCHAR(100) NOT NULL UNIQUE,
    "Resource" VARCHAR(50) NOT NULL,
    "Action" VARCHAR(50) NOT NULL,
    "Description" VARCHAR(500),
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT "chk_permission_unique" UNIQUE ("Resource", "Action")
);

CREATE INDEX "idx_permissions_resource" ON "Permissions"("Resource");

-- UserRoles Table
CREATE TABLE IF NOT EXISTS "UserRoles" (
    "UserRoleId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" UUID NOT NULL,
    "RoleId" UUID NOT NULL,
    "AssignedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "AssignedBy" UUID,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT "fk_userroles_user" FOREIGN KEY ("UserId") REFERENCES "Users"("UserId") ON DELETE CASCADE,
    CONSTRAINT "fk_userroles_role" FOREIGN KEY ("RoleId") REFERENCES "Roles"("RoleId") ON DELETE CASCADE,
    CONSTRAINT "fk_userroles_assignedby" FOREIGN KEY ("AssignedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "uq_user_role" UNIQUE ("UserId", "RoleId")
);

CREATE INDEX "idx_userroles_userid" ON "UserRoles"("UserId");
CREATE INDEX "idx_userroles_roleid" ON "UserRoles"("RoleId");

-- RolePermissions Table
CREATE TABLE IF NOT EXISTS "RolePermissions" (
    "RolePermissionId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "RoleId" UUID NOT NULL,
    "PermissionId" UUID NOT NULL,
    "GrantedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "GrantedBy" UUID,
    
    CONSTRAINT "fk_rolepermissions_role" FOREIGN KEY ("RoleId") REFERENCES "Roles"("RoleId") ON DELETE CASCADE,
    CONSTRAINT "fk_rolepermissions_permission" FOREIGN KEY ("PermissionId") REFERENCES "Permissions"("PermissionId") ON DELETE CASCADE,
    CONSTRAINT "fk_rolepermissions_grantedby" FOREIGN KEY ("GrantedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "uq_role_permission" UNIQUE ("RoleId", "PermissionId")
);

CREATE INDEX "idx_rolepermissions_roleid" ON "RolePermissions"("RoleId");

-- Certificates Table
CREATE TABLE IF NOT EXISTS "Certificates" (
    "CertificateId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(1000),
    "Thumbprint" VARCHAR(128) NOT NULL UNIQUE,
    "Subject" VARCHAR(500) NOT NULL,
    "Issuer" VARCHAR(500) NOT NULL,
    "SerialNumber" VARCHAR(100) NOT NULL,
    "Algorithm" VARCHAR(50) NOT NULL,
    "KeySize" INT NOT NULL,
    "NotBefore" TIMESTAMP WITH TIME ZONE NOT NULL,
    "NotAfter" TIMESTAMP WITH TIME ZONE NOT NULL,
    "Status" VARCHAR(20) DEFAULT 'Active' NOT NULL,
    "CertificateData" TEXT NOT NULL,
    "PrivateKeyEncrypted" BYTEA,
    "IsForSigning" BOOLEAN DEFAULT FALSE NOT NULL,
    "IsForEncryption" BOOLEAN DEFAULT TRUE NOT NULL,
    "UploadedBy" UUID NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "RevokedAt" TIMESTAMP WITH TIME ZONE,
    "RevokedBy" UUID,
    "RevokedReason" VARCHAR(500),
    "DeletedAt" TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT "fk_certificates_uploadedby" FOREIGN KEY ("UploadedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "fk_certificates_revokedby" FOREIGN KEY ("RevokedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "chk_certificate_status" CHECK ("Status" IN ('Active', 'Expired', 'Revoked', 'Pending')),
    CONSTRAINT "chk_validity_dates" CHECK ("NotBefore" < "NotAfter"),
    CONSTRAINT "chk_keysize_valid" CHECK ("KeySize" >= 2048)
);

CREATE INDEX "idx_certificates_thumbprint" ON "Certificates"("Thumbprint");
CREATE INDEX "idx_certificates_status" ON "Certificates"("Status") WHERE "DeletedAt" IS NULL;
CREATE INDEX "idx_certificates_notafter" ON "Certificates"("NotAfter") WHERE "Status" = 'Active';
CREATE INDEX "idx_certificates_uploadedby" ON "Certificates"("UploadedBy");

-- Keys Table
CREATE TABLE IF NOT EXISTS "Keys" (
    "KeyId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" VARCHAR(200) NOT NULL,
    "Description" VARCHAR(1000),
    "KeyType" VARCHAR(50) NOT NULL,
    "EncryptedValue" BYTEA NOT NULL,
    "EncryptionIV" BYTEA NOT NULL,
    "EncryptionTag" BYTEA NOT NULL,
    "CertificateId" UUID NOT NULL,
    "Version" INT DEFAULT 1 NOT NULL,
    "Status" VARCHAR(20) DEFAULT 'Active' NOT NULL,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE,
    "OwnerUserId" UUID NOT NULL,
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "CreatedBy" UUID NOT NULL,
    "UpdatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "UpdatedBy" UUID,
    "RevokedAt" TIMESTAMP WITH TIME ZONE,
    "RevokedBy" UUID,
    "RevokedReason" VARCHAR(500),
    "DeletedAt" TIMESTAMP WITH TIME ZONE,
    "LastAccessedAt" TIMESTAMP WITH TIME ZONE,
    "AccessCount" BIGINT DEFAULT 0 NOT NULL,
    
    CONSTRAINT "fk_keys_certificate" FOREIGN KEY ("CertificateId") REFERENCES "Certificates"("CertificateId"),
    CONSTRAINT "fk_keys_owner" FOREIGN KEY ("OwnerUserId") REFERENCES "Users"("UserId"),
    CONSTRAINT "fk_keys_createdby" FOREIGN KEY ("CreatedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "fk_keys_updatedby" FOREIGN KEY ("UpdatedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "fk_keys_revokedby" FOREIGN KEY ("RevokedBy") REFERENCES "Users"("UserId"),
    CONSTRAINT "chk_key_status" CHECK ("Status" IN ('Active', 'Expired', 'Revoked', 'Archived')),
    CONSTRAINT "chk_version_positive" CHECK ("Version" > 0),
    CONSTRAINT "chk_access_count_non_negative" CHECK ("AccessCount" >= 0)
);

CREATE INDEX "idx_keys_name" ON "Keys"("Name") WHERE "DeletedAt" IS NULL;
CREATE INDEX "idx_keys_status" ON "Keys"("Status") WHERE "DeletedAt" IS NULL;
CREATE INDEX "idx_keys_certificateid" ON "Keys"("CertificateId");
CREATE INDEX "idx_keys_owneruserid" ON "Keys"("OwnerUserId");
CREATE INDEX "idx_keys_expiresat" ON "Keys"("ExpiresAt") WHERE "Status" = 'Active';
CREATE INDEX "idx_keys_lastaccessedat" ON "Keys"("LastAccessedAt");

-- KeyAccessLogs Table
CREATE TABLE IF NOT EXISTS "KeyAccessLogs" (
    "AccessLogId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "KeyId" UUID NOT NULL,
    "AccessedBy" UUID NOT NULL,
    "AccessedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "AccessMethod" VARCHAR(50) NOT NULL,
    "IPAddress" INET,
    "UserAgent" VARCHAR(500),
    "IsSuccessful" BOOLEAN NOT NULL,
    "FailureReason" VARCHAR(500),
    
    CONSTRAINT "fk_keyaccesslogs_key" FOREIGN KEY ("KeyId") REFERENCES "Keys"("KeyId") ON DELETE CASCADE,
    CONSTRAINT "fk_keyaccesslogs_user" FOREIGN KEY ("AccessedBy") REFERENCES "Users"("UserId")
);

CREATE INDEX "idx_keyaccesslogs_keyid" ON "KeyAccessLogs"("KeyId");
CREATE INDEX "idx_keyaccesslogs_accessedby" ON "KeyAccessLogs"("AccessedBy");
CREATE INDEX "idx_keyaccesslogs_accessedat" ON "KeyAccessLogs"("AccessedAt" DESC);
CREATE INDEX "idx_keyaccesslogs_ipaddress" ON "KeyAccessLogs"("IPAddress");

-- UserSessions Table
CREATE TABLE IF NOT EXISTS "UserSessions" (
    "SessionId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" UUID NOT NULL,
    "RefreshToken" VARCHAR(512) NOT NULL UNIQUE,
    "IPAddress" INET,
    "UserAgent" VARCHAR(500),
    "CreatedAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "ExpiresAt" TIMESTAMP WITH TIME ZONE NOT NULL,
    "RevokedAt" TIMESTAMP WITH TIME ZONE,
    "LastActivityAt" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT "fk_usersessions_user" FOREIGN KEY ("UserId") REFERENCES "Users"("UserId") ON DELETE CASCADE
);

CREATE INDEX "idx_usersessions_userid" ON "UserSessions"("UserId");
CREATE INDEX "idx_usersessions_refreshtoken" ON "UserSessions"("RefreshToken") WHERE "RevokedAt" IS NULL;
CREATE INDEX "idx_usersessions_expiresat" ON "UserSessions"("ExpiresAt");

-- AuditTrails Table
CREATE TABLE IF NOT EXISTS "AuditTrails" (
    "AuditId" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId" UUID,
    "Action" VARCHAR(100) NOT NULL,
    "Resource" VARCHAR(50) NOT NULL,
    "ResourceId" UUID,
    "Details" JSONB,
    "IPAddress" INET,
    "UserAgent" VARCHAR(500),
    "Timestamp" TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    "Severity" VARCHAR(20) DEFAULT 'Info' NOT NULL,
    
    CONSTRAINT "fk_audittrails_user" FOREIGN KEY ("UserId") REFERENCES "Users"("UserId"),
    CONSTRAINT "chk_severity" CHECK ("Severity" IN ('Info', 'Warning', 'Critical'))
);

CREATE INDEX "idx_audittrails_userid" ON "AuditTrails"("UserId");
CREATE INDEX "idx_audittrails_timestamp" ON "AuditTrails"("Timestamp" DESC);
CREATE INDEX "idx_audittrails_action" ON "AuditTrails"("Action");
CREATE INDEX "idx_audittrails_resource" ON "AuditTrails"("Resource", "ResourceId");
CREATE INDEX "idx_audittrails_severity" ON "AuditTrails"("Severity");

-- ============================================
-- FUNCTIONS & TRIGGERS
-- ============================================

-- Auto-update UpdatedAt trigger function
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW."UpdatedAt" = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply UpdatedAt trigger to tables
CREATE TRIGGER tr_users_updated_at BEFORE UPDATE ON "Users"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_certificates_updated_at BEFORE UPDATE ON "Certificates"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_keys_updated_at BEFORE UPDATE ON "Keys"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_roles_updated_at BEFORE UPDATE ON "Roles"
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

-- ============================================
-- SEED DATA
-- ============================================

-- Insert system roles
INSERT INTO "Roles" ("RoleName", "Description", "IsSystem") VALUES
('Admin', 'Full system access', TRUE),
('Client', 'Client user with key access', TRUE),
('Service', 'Service account for API integration', TRUE)
ON CONFLICT ("RoleName") DO NOTHING;

-- Insert permissions
INSERT INTO "Permissions" ("PermissionName", "Resource", "Action", "Description") VALUES
-- Certificate permissions
('Certificate.Create', 'Certificate', 'Create', 'Upload/create new certificates'),
('Certificate.Read', 'Certificate', 'Read', 'View certificate details'),
('Certificate.Update', 'Certificate', 'Update', 'Update certificate metadata'),
('Certificate.Delete', 'Certificate', 'Delete', 'Delete/revoke certificates'),
-- Key permissions
('Key.Create', 'Key', 'Create', 'Create new keys'),
('Key.Read', 'Key', 'Read', 'View key metadata'),
('Key.Retrieve', 'Key', 'Retrieve', 'Retrieve decrypted key value'),
('Key.Update', 'Key', 'Update', 'Update key metadata'),
('Key.Delete', 'Key', 'Delete', 'Delete keys'),
-- User permissions
('User.Create', 'User', 'Create', 'Create new users'),
('User.Read', 'User', 'Read', 'View user details'),
('User.Update', 'User', 'Update', 'Update user information'),
('User.Delete', 'User', 'Delete', 'Delete users'),
-- Role permissions
('Role.Create', 'Role', 'Create', 'Create new roles'),
('Role.Read', 'Role', 'Read', 'View role details'),
('Role.Update', 'Role', 'Update', 'Update role permissions'),
('Role.Delete', 'Role', 'Delete', 'Delete roles'),
-- Audit permissions
('Audit.Read', 'Audit', 'Read', 'View audit logs')
ON CONFLICT ("PermissionName") DO NOTHING;

-- Assign all permissions to Admin role
INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
SELECT 
    r."RoleId", 
    p."PermissionId"
FROM "Roles" r
CROSS JOIN "Permissions" p
WHERE r."RoleName" = 'Admin'
ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

-- Assign limited permissions to Client role
INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
SELECT 
    r."RoleId", 
    p."PermissionId"
FROM "Roles" r
CROSS JOIN "Permissions" p
WHERE r."RoleName" = 'Client' 
  AND p."PermissionName" IN ('Key.Read', 'Key.Retrieve', 'Key.Create', 'Certificate.Read', 'User.Read')
ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

-- Assign API permissions to Service role
INSERT INTO "RolePermissions" ("RoleId", "PermissionId")
SELECT 
    r."RoleId", 
    p."PermissionId"
FROM "Roles" r
CROSS JOIN "Permissions" p
WHERE r."RoleName" = 'Service' 
  AND p."PermissionName" IN ('Key.Retrieve', 'Key.Read', 'Certificate.Read')
ON CONFLICT ("RoleId", "PermissionId") DO NOTHING;

-- Create default admin user
-- Password: Admin@123 (BCrypt hashed with cost 12)
INSERT INTO "Users" ("UserId", "Username", "Email", "PasswordHash", "FirstName", "LastName", "IsActive", "IsEmailVerified")
VALUES (
    '00000000-0000-0000-0000-000000000001'::UUID,
    'admin',
    'admin@securebox.local',
    '$2a$12$LQKYm3YBkfZq3V5x2X9xV.NZp6QxJQR8gLYJQ3K6nPqE2wL3WxK/W',
    'System',
    'Administrator',
    TRUE,
    TRUE
)
ON CONFLICT ("Username") DO NOTHING;

-- Assign Admin role to admin user
INSERT INTO "UserRoles" ("UserId", "RoleId")
SELECT 
    '00000000-0000-0000-0000-000000000001'::UUID,
    r."RoleId"
FROM "Roles" r
WHERE r."RoleName" = 'Admin'
ON CONFLICT ("UserId", "RoleId") DO NOTHING;

-- ============================================
-- VIEWS
-- ============================================

CREATE OR REPLACE VIEW "vw_UserPermissions" AS
SELECT 
    u."UserId",
    u."Username",
    u."Email",
    r."RoleId",
    r."RoleName",
    p."PermissionId",
    p."PermissionName",
    p."Resource",
    p."Action"
FROM "Users" u
INNER JOIN "UserRoles" ur ON u."UserId" = ur."UserId"
INNER JOIN "Roles" r ON ur."RoleId" = r."RoleId"
INNER JOIN "RolePermissions" rp ON r."RoleId" = rp."RoleId"
INNER JOIN "Permissions" p ON rp."PermissionId" = p."PermissionId"
WHERE u."IsActive" = TRUE
  AND u."DeletedAt" IS NULL
  AND r."DeletedAt" IS NULL
  AND (ur."ExpiresAt" IS NULL OR ur."ExpiresAt" > CURRENT_TIMESTAMP);

CREATE OR REPLACE VIEW "vw_ActiveCertificates" AS
SELECT 
    c."CertificateId",
    c."Name",
    c."Thumbprint",
    c."Subject",
    c."Issuer",
    c."NotBefore",
    c."NotAfter",
    c."Status",
    u."Username" AS "UploadedByUsername",
    CASE 
        WHEN c."NotAfter" < CURRENT_TIMESTAMP THEN 'Expired'
        WHEN c."NotAfter" < CURRENT_TIMESTAMP + INTERVAL '30 days' THEN 'Expiring Soon'
        ELSE 'Valid'
    END AS "ValidityStatus",
    c."CreatedAt"
FROM "Certificates" c
INNER JOIN "Users" u ON c."UploadedBy" = u."UserId"
WHERE c."Status" = 'Active'
  AND c."DeletedAt" IS NULL;

-- Grant permissions to securebox_user
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO securebox_user;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO securebox_user;
GRANT EXECUTE ON ALL FUNCTIONS IN SCHEMA public TO securebox_user;

