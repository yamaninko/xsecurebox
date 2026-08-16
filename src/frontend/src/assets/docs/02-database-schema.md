# PostgreSQL Veritabanı Şeması

## Genel Bakış

Secure Box sistemi için PostgreSQL veritabanı şeması. Tüm tablolar normalizasyon prensiplerine uygun olarak tasarlanmıştır. Audit trail, foreign key constraints, indexes ve constraints detaylı olarak tanımlanmıştır.

---

## 1. ERD (Entity Relationship Diagram - Text)

```
Users ──1:N── UserRoles ──N:1── Roles
  │                              │
  │                              │
  │                         1:N  │
  │                       RolePermissions
  │                              │
  │                            N:1
  │                         Permissions
  │
  ├──1:N── Certificates
  │           │
  │           │1:N
  │           └───── Keys
  │                    │
  │                    │1:N
  │                    └───── KeyAccessLogs
  │
  └──1:N── AuditTrails
  └──1:N── UserSessions
```

---

## 2. Tablo Tanımları

### 2.1 Users (Kullanıcılar)

Sistemdeki tüm kullanıcı bilgilerini saklar.

```sql
CREATE TABLE Users (
    UserId              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Username            VARCHAR(100) NOT NULL UNIQUE,
    Email               VARCHAR(255) NOT NULL UNIQUE,
    PasswordHash        VARCHAR(512) NOT NULL,
    FirstName           VARCHAR(100),
    LastName            VARCHAR(100),
    IsActive            BOOLEAN DEFAULT TRUE NOT NULL,
    IsEmailVerified     BOOLEAN DEFAULT FALSE NOT NULL,
    MustChangePassword  BOOLEAN DEFAULT FALSE NOT NULL,
    FailedLoginAttempts INT DEFAULT 0 NOT NULL,
    LastLoginAt         TIMESTAMP WITH TIME ZONE,
    LockedOutUntil      TIMESTAMP WITH TIME ZONE,
    CreatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CreatedBy           UUID,
    UpdatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UpdatedBy           UUID,
    DeletedAt           TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT chk_email_format CHECK (Email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$'),
    CONSTRAINT chk_failed_login_non_negative CHECK (FailedLoginAttempts >= 0)
);

CREATE INDEX idx_users_username ON Users(Username) WHERE DeletedAt IS NULL;
CREATE INDEX idx_users_email ON Users(Email) WHERE DeletedAt IS NULL;
CREATE INDEX idx_users_isactive ON Users(IsActive) WHERE DeletedAt IS NULL;
```

**Açıklama**:
- `PasswordHash`: BCrypt veya Argon2 ile hashlenmiş şifre
- `FailedLoginAttempts`: Brute-force koruması için
- `LockedOutUntil`: Account lockout mekanizması
- Soft delete (`DeletedAt`)

---

### 2.2 Roles (Roller)

Sistem rolleri: Admin, Client, Service.

```sql
CREATE TABLE Roles (
    RoleId      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    RoleName    VARCHAR(50) NOT NULL UNIQUE,
    Description VARCHAR(500),
    IsSystem    BOOLEAN DEFAULT FALSE NOT NULL, -- System roles cannot be deleted
    CreatedAt   TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CreatedBy   UUID,
    UpdatedAt   TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UpdatedBy   UUID,
    DeletedAt   TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT chk_rolename_not_empty CHECK (TRIM(RoleName) <> '')
);

CREATE INDEX idx_roles_rolename ON Roles(RoleName) WHERE DeletedAt IS NULL;

-- Pre-populate system roles
INSERT INTO Roles (RoleName, Description, IsSystem) VALUES
('Admin', 'Full system access', TRUE),
('Client', 'Client user with key access', TRUE),
('Service', 'Service account for API integration', TRUE);
```

---

### 2.3 Permissions (İzinler)

Granular permission tanımları.

```sql
CREATE TABLE Permissions (
    PermissionId   UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    PermissionName VARCHAR(100) NOT NULL UNIQUE,
    Resource       VARCHAR(50) NOT NULL, -- e.g., 'Certificate', 'Key', 'User'
    Action         VARCHAR(50) NOT NULL, -- e.g., 'Create', 'Read', 'Update', 'Delete'
    Description    VARCHAR(500),
    CreatedAt      TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT chk_permission_unique UNIQUE (Resource, Action)
);

CREATE INDEX idx_permissions_resource ON Permissions(Resource);

-- Pre-populate permissions
INSERT INTO Permissions (PermissionName, Resource, Action, Description) VALUES
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
('Audit.Read', 'Audit', 'Read', 'View audit logs');
```

---

### 2.4 UserRoles (Kullanıcı-Rol İlişkisi)

Many-to-many relationship.

```sql
CREATE TABLE UserRoles (
    UserRoleId  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId      UUID NOT NULL,
    RoleId      UUID NOT NULL,
    AssignedAt  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    AssignedBy  UUID,
    ExpiresAt   TIMESTAMP WITH TIME ZONE, -- Optional: time-limited roles
    
    CONSTRAINT fk_userroles_user FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE,
    CONSTRAINT fk_userroles_role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId) ON DELETE CASCADE,
    CONSTRAINT fk_userroles_assignedby FOREIGN KEY (AssignedBy) REFERENCES Users(UserId),
    CONSTRAINT uq_user_role UNIQUE (UserId, RoleId)
);

CREATE INDEX idx_userroles_userid ON UserRoles(UserId);
CREATE INDEX idx_userroles_roleid ON UserRoles(RoleId);
```

---

### 2.5 RolePermissions (Rol-İzin İlişkisi)

Many-to-many relationship.

```sql
CREATE TABLE RolePermissions (
    RolePermissionId UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    RoleId           UUID NOT NULL,
    PermissionId     UUID NOT NULL,
    GrantedAt        TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    GrantedBy        UUID,
    
    CONSTRAINT fk_rolepermissions_role FOREIGN KEY (RoleId) REFERENCES Roles(RoleId) ON DELETE CASCADE,
    CONSTRAINT fk_rolepermissions_permission FOREIGN KEY (PermissionId) REFERENCES Permissions(PermissionId) ON DELETE CASCADE,
    CONSTRAINT fk_rolepermissions_grantedby FOREIGN KEY (GrantedBy) REFERENCES Users(UserId),
    CONSTRAINT uq_role_permission UNIQUE (RoleId, PermissionId)
);

CREATE INDEX idx_rolepermissions_roleid ON RolePermissions(RoleId);

-- Pre-populate role permissions (Admin gets all)
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT 
    r.RoleId, 
    p.PermissionId
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleName = 'Admin';

-- Client role: limited permissions
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT 
    r.RoleId, 
    p.PermissionId
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleName = 'Client' 
  AND p.PermissionName IN ('Key.Read', 'Key.Retrieve', 'Certificate.Read', 'User.Read');

-- Service role: API integration permissions
INSERT INTO RolePermissions (RoleId, PermissionId)
SELECT 
    r.RoleId, 
    p.PermissionId
FROM Roles r
CROSS JOIN Permissions p
WHERE r.RoleName = 'Service' 
  AND p.PermissionName IN ('Key.Retrieve', 'Certificate.Read');
```

---

### 2.6 Certificates (Sertifikalar)

X.509 sertifika bilgileri.

```sql
CREATE TABLE Certificates (
    CertificateId       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name                VARCHAR(200) NOT NULL,
    Description         VARCHAR(1000),
    Thumbprint          VARCHAR(128) NOT NULL UNIQUE, -- SHA-256 hash
    Subject             VARCHAR(500) NOT NULL,
    Issuer              VARCHAR(500) NOT NULL,
    SerialNumber        VARCHAR(100) NOT NULL,
    Algorithm           VARCHAR(50) NOT NULL, -- e.g., 'RSA', 'ECC'
    KeySize             INT NOT NULL, -- e.g., 2048, 4096
    NotBefore           TIMESTAMP WITH TIME ZONE NOT NULL,
    NotAfter            TIMESTAMP WITH TIME ZONE NOT NULL,
    Status              VARCHAR(20) DEFAULT 'Active' NOT NULL, -- Active, Expired, Revoked
    CertificateData     TEXT NOT NULL, -- PEM encoded certificate
    PrivateKeyEncrypted BYTEA, -- Encrypted private key (optional, if stored)
    IsForSigning        BOOLEAN DEFAULT FALSE NOT NULL,
    IsForEncryption     BOOLEAN DEFAULT TRUE NOT NULL,
    UploadedBy          UUID NOT NULL,
    CreatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UpdatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    RevokedAt           TIMESTAMP WITH TIME ZONE,
    RevokedBy           UUID,
    RevokedReason       VARCHAR(500),
    DeletedAt           TIMESTAMP WITH TIME ZONE,
    
    CONSTRAINT fk_certificates_uploadedby FOREIGN KEY (UploadedBy) REFERENCES Users(UserId),
    CONSTRAINT fk_certificates_revokedby FOREIGN KEY (RevokedBy) REFERENCES Users(UserId),
    CONSTRAINT chk_certificate_status CHECK (Status IN ('Active', 'Expired', 'Revoked', 'Pending')),
    CONSTRAINT chk_validity_dates CHECK (NotBefore < NotAfter),
    CONSTRAINT chk_keysize_valid CHECK (KeySize >= 2048)
);

CREATE INDEX idx_certificates_thumbprint ON Certificates(Thumbprint);
CREATE INDEX idx_certificates_status ON Certificates(Status) WHERE DeletedAt IS NULL;
CREATE INDEX idx_certificates_notafter ON Certificates(NotAfter) WHERE Status = 'Active';
CREATE INDEX idx_certificates_uploadedby ON Certificates(UploadedBy);
```

**Açıklama**:
- `Thumbprint`: Sertifika unique identifier (SHA-256)
- `NotAfter`: Expiration check için index
- `PrivateKeyEncrypted`: Private key saklanırsa (highly sensitive), şifrelenmiş olarak
- `Status`: Active, Expired, Revoked, Pending

---

### 2.7 Keys (Anahtarlar)

Şifrelenmiş olarak saklanan kritik anahtarlar.

```sql
CREATE TABLE Keys (
    KeyId               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    Name                VARCHAR(200) NOT NULL,
    Description         VARCHAR(1000),
    KeyType             VARCHAR(50) NOT NULL, -- e.g., 'API_KEY', 'DATABASE_PASSWORD', 'SECRET'
    EncryptedValue      BYTEA NOT NULL, -- AES-256-GCM encrypted key
    EncryptionIV        BYTEA NOT NULL, -- Initialization Vector (16 bytes)
    EncryptionTag       BYTEA NOT NULL, -- Authentication Tag (16 bytes)
    CertificateId       UUID NOT NULL, -- Certificate used for encryption
    Version             INT DEFAULT 1 NOT NULL, -- Key versioning for rotation
    Status              VARCHAR(20) DEFAULT 'Active' NOT NULL, -- Active, Expired, Revoked, Archived
    ExpiresAt           TIMESTAMP WITH TIME ZONE, -- Optional expiration
    OwnerUserId         UUID NOT NULL,
    CreatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    CreatedBy           UUID NOT NULL,
    UpdatedAt           TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    UpdatedBy           UUID,
    RevokedAt           TIMESTAMP WITH TIME ZONE,
    RevokedBy           UUID,
    RevokedReason       VARCHAR(500),
    DeletedAt           TIMESTAMP WITH TIME ZONE,
    LastAccessedAt      TIMESTAMP WITH TIME ZONE,
    AccessCount         BIGINT DEFAULT 0 NOT NULL,
    
    CONSTRAINT fk_keys_certificate FOREIGN KEY (CertificateId) REFERENCES Certificates(CertificateId),
    CONSTRAINT fk_keys_owner FOREIGN KEY (OwnerUserId) REFERENCES Users(UserId),
    CONSTRAINT fk_keys_createdby FOREIGN KEY (CreatedBy) REFERENCES Users(UserId),
    CONSTRAINT fk_keys_updatedby FOREIGN KEY (UpdatedBy) REFERENCES Users(UserId),
    CONSTRAINT fk_keys_revokedby FOREIGN KEY (RevokedBy) REFERENCES Users(UserId),
    CONSTRAINT chk_key_status CHECK (Status IN ('Active', 'Expired', 'Revoked', 'Archived')),
    CONSTRAINT chk_version_positive CHECK (Version > 0),
    CONSTRAINT chk_access_count_non_negative CHECK (AccessCount >= 0)
);

CREATE INDEX idx_keys_name ON Keys(Name) WHERE DeletedAt IS NULL;
CREATE INDEX idx_keys_status ON Keys(Status) WHERE DeletedAt IS NULL;
CREATE INDEX idx_keys_certificateid ON Keys(CertificateId);
CREATE INDEX idx_keys_owneruserid ON Keys(OwnerUserId);
CREATE INDEX idx_keys_expiresat ON Keys(ExpiresAt) WHERE Status = 'Active';
CREATE INDEX idx_keys_lastaccessedat ON Keys(LastAccessedAt);
```

**Açıklama**:
- `EncryptedValue`: AES-256-GCM ile şifrelenmiş anahtar
- `EncryptionIV`, `EncryptionTag`: GCM mode için gerekli
- `Version`: Key rotation için (aynı key'in farklı versiyonları)
- `AccessCount`: Monitoring ve analytics için

---

### 2.8 KeyAccessLogs (Anahtar Erişim Logları)

Her key retrieval işlemi kaydedilir (audit trail).

```sql
CREATE TABLE KeyAccessLogs (
    AccessLogId     UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    KeyId           UUID NOT NULL,
    AccessedBy      UUID NOT NULL,
    AccessedAt      TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    AccessMethod    VARCHAR(50) NOT NULL, -- 'API', 'Portal', 'Service'
    IPAddress       INET,
    UserAgent       VARCHAR(500),
    IsSuccessful    BOOLEAN NOT NULL,
    FailureReason   VARCHAR(500),
    
    CONSTRAINT fk_keyaccesslogs_key FOREIGN KEY (KeyId) REFERENCES Keys(KeyId) ON DELETE CASCADE,
    CONSTRAINT fk_keyaccesslogs_user FOREIGN KEY (AccessedBy) REFERENCES Users(UserId)
);

-- Partition by time for performance (monthly partitions)
CREATE INDEX idx_keyaccesslogs_keyid ON KeyAccessLogs(KeyId);
CREATE INDEX idx_keyaccesslogs_accessedby ON KeyAccessLogs(AccessedBy);
CREATE INDEX idx_keyaccesslogs_accessedat ON KeyAccessLogs(AccessedAt DESC);
CREATE INDEX idx_keyaccesslogs_ipaddress ON KeyAccessLogs(IPAddress);
```

**Açıklama**:
- Tamper-proof audit için (immutable, no updates)
- Compliance ve forensics için kritik
- Time-series data (partitioning önerilir production'da)

---

### 2.9 UserSessions (Kullanıcı Oturumları)

JWT refresh token tracking.

```sql
CREATE TABLE UserSessions (
    SessionId       UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId          UUID NOT NULL,
    RefreshToken    VARCHAR(512) NOT NULL UNIQUE,
    IPAddress       INET,
    UserAgent       VARCHAR(500),
    CreatedAt       TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    ExpiresAt       TIMESTAMP WITH TIME ZONE NOT NULL,
    RevokedAt       TIMESTAMP WITH TIME ZONE,
    LastActivityAt  TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    
    CONSTRAINT fk_usersessions_user FOREIGN KEY (UserId) REFERENCES Users(UserId) ON DELETE CASCADE
);

CREATE INDEX idx_usersessions_userid ON UserSessions(UserId);
CREATE INDEX idx_usersessions_refreshtoken ON UserSessions(RefreshToken) WHERE RevokedAt IS NULL;
CREATE INDEX idx_usersessions_expiresat ON UserSessions(ExpiresAt);

-- Auto-cleanup expired sessions (PostgreSQL job veya application-side)
```

---

### 2.10 AuditTrails (Audit İzleme)

Tüm kritik işlemler için genel audit trail (summary level).

```sql
CREATE TABLE AuditTrails (
    AuditId         UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    UserId          UUID,
    Action          VARCHAR(100) NOT NULL, -- e.g., 'User.Created', 'Key.Accessed', 'Certificate.Revoked'
    Resource        VARCHAR(50) NOT NULL, -- 'User', 'Key', 'Certificate', etc.
    ResourceId      UUID, -- ID of the affected resource
    Details         JSONB, -- Additional details (before/after values, etc.)
    IPAddress       INET,
    UserAgent       VARCHAR(500),
    Timestamp       TIMESTAMP WITH TIME ZONE DEFAULT CURRENT_TIMESTAMP NOT NULL,
    Severity        VARCHAR(20) DEFAULT 'Info' NOT NULL, -- Info, Warning, Critical
    
    CONSTRAINT fk_audittrails_user FOREIGN KEY (UserId) REFERENCES Users(UserId),
    CONSTRAINT chk_severity CHECK (Severity IN ('Info', 'Warning', 'Critical'))
);

CREATE INDEX idx_audittrails_userid ON AuditTrails(UserId);
CREATE INDEX idx_audittrails_timestamp ON AuditTrails(Timestamp DESC);
CREATE INDEX idx_audittrails_action ON AuditTrails(Action);
CREATE INDEX idx_audittrails_resource ON AuditTrails(Resource, ResourceId);
CREATE INDEX idx_audittrails_severity ON AuditTrails(Severity);
```

**Açıklama**:
- Summary level (detaylı loglar MongoDB'de)
- JSONB field: flexible schema (before/after values)
- Immutable (no updates/deletes)

---

## 3. Views (Görünümler)

### 3.1 vw_UserPermissions

Kullanıcının tüm izinlerini dönen view (join heavy query optimization).

```sql
CREATE OR REPLACE VIEW vw_UserPermissions AS
SELECT 
    u.UserId,
    u.Username,
    u.Email,
    r.RoleId,
    r.RoleName,
    p.PermissionId,
    p.PermissionName,
    p.Resource,
    p.Action
FROM Users u
INNER JOIN UserRoles ur ON u.UserId = ur.UserId
INNER JOIN Roles r ON ur.RoleId = r.RoleId
INNER JOIN RolePermissions rp ON r.RoleId = rp.RoleId
INNER JOIN Permissions p ON rp.PermissionId = p.PermissionId
WHERE u.IsActive = TRUE
  AND u.DeletedAt IS NULL
  AND r.DeletedAt IS NULL
  AND (ur.ExpiresAt IS NULL OR ur.ExpiresAt > CURRENT_TIMESTAMP);
```

---

### 3.2 vw_ActiveCertificates

Aktif ve süresi dolmamış sertifikalar.

```sql
CREATE OR REPLACE VIEW vw_ActiveCertificates AS
SELECT 
    c.CertificateId,
    c.Name,
    c.Thumbprint,
    c.Subject,
    c.Issuer,
    c.NotBefore,
    c.NotAfter,
    c.Status,
    u.Username AS UploadedByUsername,
    CASE 
        WHEN c.NotAfter < CURRENT_TIMESTAMP THEN 'Expired'
        WHEN c.NotAfter < CURRENT_TIMESTAMP + INTERVAL '30 days' THEN 'Expiring Soon'
        ELSE 'Valid'
    END AS ValidityStatus,
    c.CreatedAt
FROM Certificates c
INNER JOIN Users u ON c.UploadedBy = u.UserId
WHERE c.Status = 'Active'
  AND c.DeletedAt IS NULL;
```

---

### 3.3 vw_KeyStatistics

Key'ler için istatistikler.

```sql
CREATE OR REPLACE VIEW vw_KeyStatistics AS
SELECT 
    k.KeyId,
    k.Name,
    k.KeyType,
    k.Status,
    u.Username AS OwnerUsername,
    k.AccessCount,
    k.LastAccessedAt,
    c.Name AS CertificateName,
    c.Thumbprint AS CertificateThumbprint,
    CASE 
        WHEN k.ExpiresAt IS NULL THEN 'No Expiration'
        WHEN k.ExpiresAt < CURRENT_TIMESTAMP THEN 'Expired'
        WHEN k.ExpiresAt < CURRENT_TIMESTAMP + INTERVAL '7 days' THEN 'Expiring Soon'
        ELSE 'Valid'
    END AS ExpirationStatus,
    k.CreatedAt
FROM Keys k
INNER JOIN Users u ON k.OwnerUserId = u.UserId
INNER JOIN Certificates c ON k.CertificateId = c.CertificateId
WHERE k.DeletedAt IS NULL;
```

---

## 4. Functions & Stored Procedures

### 4.1 Update Timestamp Trigger

Otomatik `UpdatedAt` güncelleme.

```sql
CREATE OR REPLACE FUNCTION update_updated_at_column()
RETURNS TRIGGER AS $$
BEGIN
    NEW.UpdatedAt = CURRENT_TIMESTAMP;
    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

-- Apply to all relevant tables
CREATE TRIGGER tr_users_updated_at BEFORE UPDATE ON Users
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_certificates_updated_at BEFORE UPDATE ON Certificates
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_keys_updated_at BEFORE UPDATE ON Keys
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER tr_roles_updated_at BEFORE UPDATE ON Roles
    FOR EACH ROW EXECUTE FUNCTION update_updated_at_column();
```

---

### 4.2 Certificate Expiry Check Function

Süresi dolmuş sertifikaları otomatik 'Expired' yapma.

```sql
CREATE OR REPLACE FUNCTION check_certificate_expiry()
RETURNS void AS $$
BEGIN
    UPDATE Certificates
    SET Status = 'Expired'
    WHERE Status = 'Active'
      AND NotAfter < CURRENT_TIMESTAMP;
END;
$$ LANGUAGE plpgsql;

-- Scheduled job (pg_cron extension veya application-side)
-- SELECT cron.schedule('check-cert-expiry', '0 2 * * *', 'SELECT check_certificate_expiry();');
```

---

### 4.3 Key Access Logging Function

Key erişimi otomatik loglama.

```sql
CREATE OR REPLACE FUNCTION log_key_access(
    p_key_id UUID,
    p_accessed_by UUID,
    p_access_method VARCHAR(50),
    p_ip_address INET,
    p_user_agent VARCHAR(500),
    p_is_successful BOOLEAN,
    p_failure_reason VARCHAR(500) DEFAULT NULL
)
RETURNS void AS $$
BEGIN
    INSERT INTO KeyAccessLogs (
        KeyId, AccessedBy, AccessMethod, IPAddress, UserAgent, 
        IsSuccessful, FailureReason
    ) VALUES (
        p_key_id, p_accessed_by, p_access_method, p_ip_address, 
        p_user_agent, p_is_successful, p_failure_reason
    );
    
    -- Update key's last accessed timestamp and access count
    IF p_is_successful THEN
        UPDATE Keys
        SET LastAccessedAt = CURRENT_TIMESTAMP,
            AccessCount = AccessCount + 1
        WHERE KeyId = p_key_id;
    END IF;
END;
$$ LANGUAGE plpgsql;
```

---

## 5. Security Considerations

### 5.1 Row-Level Security (RLS)

Client kullanıcılar sadece kendi key'lerini görebilir.

```sql
ALTER TABLE Keys ENABLE ROW LEVEL SECURITY;

CREATE POLICY key_owner_policy ON Keys
    FOR ALL
    TO PUBLIC
    USING (
        OwnerUserId = current_setting('app.current_user_id')::UUID
        OR EXISTS (
            SELECT 1 FROM vw_UserPermissions
            WHERE UserId = current_setting('app.current_user_id')::UUID
              AND PermissionName = 'Key.Read'
              AND Resource = 'Key'
        )
    );

-- Application her request'te SET LOCAL app.current_user_id = '<user_id>'; çalıştırır
```

---

### 5.2 Encryption at Rest

PostgreSQL PGCRYPTO extension ile sensitive column encryption.

```sql
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Example: Additional encryption layer (optional)
-- UPDATE Keys SET EncryptedValue = pgp_sym_encrypt(EncryptedValue::TEXT, 'master_key');
```

---

### 5.3 Audit Trigger

Tüm DELETE/UPDATE işlemlerini audit trail'e yaz.

```sql
CREATE OR REPLACE FUNCTION audit_changes()
RETURNS TRIGGER AS $$
BEGIN
    IF (TG_OP = 'DELETE') THEN
        INSERT INTO AuditTrails (UserId, Action, Resource, ResourceId, Details)
        VALUES (
            current_setting('app.current_user_id', TRUE)::UUID,
            TG_TABLE_NAME || '.Deleted',
            TG_TABLE_NAME,
            OLD.KeyId, -- Adapt based on table
            jsonb_build_object('old_record', row_to_json(OLD))
        );
        RETURN OLD;
    ELSIF (TG_OP = 'UPDATE') THEN
        INSERT INTO AuditTrails (UserId, Action, Resource, ResourceId, Details)
        VALUES (
            current_setting('app.current_user_id', TRUE)::UUID,
            TG_TABLE_NAME || '.Updated',
            TG_TABLE_NAME,
            NEW.KeyId,
            jsonb_build_object('old', row_to_json(OLD), 'new', row_to_json(NEW))
        );
        RETURN NEW;
    END IF;
END;
$$ LANGUAGE plpgsql;

-- Apply to critical tables
CREATE TRIGGER tr_keys_audit AFTER UPDATE OR DELETE ON Keys
    FOR EACH ROW EXECUTE FUNCTION audit_changes();
```

---

## 6. Data Retention & Archiving

### 6.1 Soft Delete Policy

- Tüm critical tablolarda soft delete (`DeletedAt`)
- Hard delete sadece retention policy sonrası (örn: 1 yıl)

### 6.2 Log Partitioning

KeyAccessLogs ve AuditTrails için monthly partitions (PostgreSQL 10+ native partitioning).

```sql
-- Example: Partition KeyAccessLogs by month
CREATE TABLE KeyAccessLogs (
    -- columns as defined above
) PARTITION BY RANGE (AccessedAt);

CREATE TABLE KeyAccessLogs_2025_01 PARTITION OF KeyAccessLogs
    FOR VALUES FROM ('2025-01-01') TO ('2025-02-01');

CREATE TABLE KeyAccessLogs_2025_02 PARTITION OF KeyAccessLogs
    FOR VALUES FROM ('2025-02-01') TO ('2025-03-01');

-- Auto-create partitions via script/cron
```

---

## 7. Sample Data (Initial Seed)

### 7.1 Default Admin User

```sql
-- Admin password is set from ADMIN_PASSWORD at runtime (not stored in this file)
INSERT INTO Users (UserId, Username, Email, PasswordHash, FirstName, LastName, IsActive, IsEmailVerified)
VALUES (
    '00000000-0000-0000-0000-000000000001'::UUID,
    'admin',
    'admin@securebox.local',
    '$2a$11$yourhashedpasswordhere', -- Replace with actual hash
    'System',
    'Administrator',
    TRUE,
    TRUE
);

-- Assign Admin role
INSERT INTO UserRoles (UserId, RoleId)
SELECT 
    '00000000-0000-0000-0000-000000000001'::UUID,
    RoleId
FROM Roles
WHERE RoleName = 'Admin';
```

---

## 8. Database Indexes Strategy

### High Priority Indexes (Already Created)
- ✅ Primary Keys (clustered indexes)
- ✅ Foreign Keys
- ✅ Unique constraints (Username, Email, Thumbprint)
- ✅ Status columns (for filtering)
- ✅ Timestamp columns (for sorting, range queries)

### Future Optimization
- **Composite indexes** için query pattern analysis
- **Partial indexes** için WHERE clauses (already implemented for DeletedAt)
- **GIN indexes** JSONB columns için (AuditTrails.Details)

---

## 9. Backup & Restore Strategy

### 9.1 Backup
```bash
# Full backup (daily)
pg_dump -h localhost -U postgres -d securebox -F c -f backup_$(date +%Y%m%d).dump

# Schema-only backup
pg_dump -h localhost -U postgres -d securebox -s -f schema.sql
```

### 9.2 Restore
```bash
pg_restore -h localhost -U postgres -d securebox -c backup_20250101.dump
```

---

## 10. Performance Considerations

### 10.1 Connection Pooling
- **Min Pool Size**: 10
- **Max Pool Size**: 50
- **Connection Timeout**: 30s

### 10.2 Query Optimization
- `EXPLAIN ANALYZE` kullanarak critical queries profiling
- Materialized views için frequently accessed aggregations
- Query result caching (Redis)

### 10.3 Monitoring
- **pg_stat_statements**: Slow query detection
- **pg_stat_user_tables**: Table statistics
- **Deadlock monitoring**: PostgreSQL logs

---

## Sonuç

Bu şema, Secure Box sisteminin tüm veri gereksinimlerini karşılayacak şekilde tasarlanmıştır:
- ✅ Normalized (3NF)
- ✅ Audit trail support
- ✅ Security (RLS, soft delete, encryption hooks)
- ✅ Performance (indexes, partitioning ready)
- ✅ Scalability (foreign key cascades, efficient queries)
- ✅ Compliance (GDPR ready with soft delete, audit logs)

