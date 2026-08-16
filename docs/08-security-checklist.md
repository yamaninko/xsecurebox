# Güvenlik Kontrol Listesi ve Key Lifecycle Policy

## 1. Güvenlik Kontrol Listesi (Security Checklist)

### 1.1 Network Security ✅

#### TLS/SSL Configuration
- [ ] **TLS 1.3 Mandatory**: Tüm external communication TLS 1.3 ile şifrelenmiş
- [ ] **Valid SSL Certificates**: Production'da geçerli SSL sertifikaları (Let's Encrypt, DigiCert, etc.)
- [ ] **HSTS Enabled**: Strict-Transport-Security header aktif (max-age=31536000)
- [ ] **Certificate Pinning**: Critical API endpoints için certificate pinning (opsiyonel)
- [ ] **Strong Cipher Suites**: Sadece güvenli cipher'lar (TLS_AES_256_GCM_SHA384, TLS_CHACHA20_POLY1305_SHA256)

#### Firewall & Network Segmentation
- [ ] **Firewall Rules**: Sadece gerekli portlar expose edilmiş (80, 443, 5432 sadece internal)
- [ ] **Network Segmentation**: Frontend, Backend, Data katmanları ayrı networkler
- [ ] **DMZ Zone**: API gateway DMZ zone'da
- [ ] **Private Subnets**: Database, Redis, MongoDB private subnet'te
- [ ] **No Direct DB Access**: Database'lere sadece API üzerinden erişim

### 1.2 Application Security ✅

#### Authentication
- [ ] **JWT Tokens**: Short-lived access tokens (15 min)
- [ ] **Refresh Tokens**: Long-lived (7 days), securely stored, rotatable
- [ ] **Token Blacklist**: Redis'te revoked token listesi
- [ ] **Password Policy**: Min 8 chars, complexity requirements (uppercase, lowercase, digit, special)
- [ ] **Bcrypt/Argon2**: Password hashing with salt (cost factor ≥ 12 for BCrypt)
- [ ] **Multi-Factor Authentication (MFA)**: MVP sonrası eklenecek (TOTP)
- [ ] **Account Lockout**: 5 failed attempts → 15 min lockout

#### Authorization
- [ ] **Role-Based Access Control (RBAC)**: Admin, Client, Service roles
- [ ] **Permission-Based**: Granular permissions (Key.Retrieve, Certificate.Create, etc.)
- [ ] **Least Privilege**: Users sadece gerekli permissionlara sahip
- [ ] **API Endpoint Protection**: Tüm endpoints authorization check
- [ ] **Row-Level Security**: PostgreSQL RLS ile kullanıcılar sadece kendi datalarını görebilir

#### Input Validation & Sanitization
- [ ] **Server-Side Validation**: Tüm user input'ları validate edilir (FluentValidation)
- [ ] **SQL Injection Prevention**: Parameterized queries (EF Core)
- [ ] **XSS Prevention**: Input sanitization, output encoding
- [ ] **CSRF Protection**: CSRF tokens (Angular HttpClient)
- [ ] **File Upload Validation**: Certificate upload'da type, size, content check
- [ ] **Max Request Size**: 10MB limit (nginx config)

#### Rate Limiting
- [ ] **Redis-Based Rate Limiting**: Per-user/IP rate limits
- [ ] **Global Limit**: 100 req/min per authenticated user
- [ ] **Critical Endpoints**: Key retrieval 10/hour for Client, 100/hour for Service
- [ ] **Login Endpoint**: 5 attempts/5 min per IP
- [ ] **DDoS Protection**: Nginx limit_req_zone

### 1.3 Data Security ✅

#### Encryption at Rest
- [ ] **Database Encryption**: PostgreSQL disk encryption (LUKS/dm-crypt)
- [ ] **MongoDB Encryption**: Encrypted storage engine
- [ ] **Redis Encryption**: RDB snapshots encrypted
- [ ] **Certificate Storage**: X.509 certificates encrypted on disk
- [ ] **Key Encryption**: AES-256-GCM with certificate-based key wrapping

#### Encryption in Transit
- [ ] **TLS Everywhere**: All service-to-service communication over TLS
- [ ] **API ↔ Database**: PostgreSQL SSL mode (require)
- [ ] **API ↔ Redis**: TLS connection
- [ ] **API ↔ RabbitMQ**: SSL/TLS enabled

#### Key Management (Application Keys)
- [ ] **Certificate-Based Encryption**: Keys encrypted with X.509 public key
- [ ] **AES-256-GCM**: Authenticated encryption with additional data (AEAD)
- [ ] **Unique IV**: Random 96-bit IV per encryption
- [ ] **Authentication Tag**: 128-bit tag for integrity
- [ ] **No Plaintext Storage**: Keys never stored in plaintext
- [ ] **Secure Deletion**: Cryptographic erasure on key deletion

#### Secrets Management (Infrastructure)
- [ ] **Environment Variables**: Sensitive configs in env vars, not code
- [ ] **Docker Secrets**: Use Docker secrets for production (not .env files)
- [ ] **HashiCorp Vault**: Centralized secret management (future enhancement)
- [ ] **No Hardcoded Secrets**: Code review to catch hardcoded passwords/keys
- [ ] **Rotate Secrets**: Database passwords rotated quarterly

### 1.4 Audit & Monitoring ✅

#### Audit Logging
- [ ] **Comprehensive Logging**: All CRUD operations logged (AuditTrails table)
- [ ] **Immutable Logs**: No updates/deletes on audit logs
- [ ] **Tamper-Proof Storage**: MongoDB logs with write-once collections
- [ ] **Key Access Logs**: Every key retrieval logged with IP, user, timestamp
- [ ] **Failed Operations**: Login failures, authorization denials logged
- [ ] **Log Retention**: 1 year retention, then archival

#### Monitoring & Alerting
- [ ] **Real-Time Monitoring**: ELK stack for log analysis
- [ ] **Security Alerts**: Failed login spikes, suspicious key access patterns
- [ ] **Certificate Expiry Alerts**: 30 days before expiration
- [ ] **System Health**: Database, Redis, RabbitMQ health checks
- [ ] **Anomaly Detection**: ML-based anomaly detection (future enhancement)
- [ ] **SIEM Integration**: Export logs to SIEM (Splunk, QRadar - future)

#### Incident Response
- [ ] **Incident Response Plan**: Documented procedures for breaches
- [ ] **Automated Lockdown**: Suspicious activity triggers account lockdown
- [ ] **Forensics Ready**: Detailed logs for post-incident analysis
- [ ] **Backup & Recovery**: Daily backups, 4-hour RTO, 1-hour RPO

### 1.5 Infrastructure Security ✅

#### Docker & Container Security
- [ ] **Non-Root Containers**: All containers run as non-root user
- [ ] **Minimal Base Images**: Alpine Linux images (smaller attack surface)
- [ ] **Image Scanning**: Trivy/Clair for vulnerability scanning
- [ ] **Read-Only Filesystems**: Containers use read-only root FS where possible
- [ ] **Resource Limits**: Memory/CPU limits to prevent resource exhaustion
- [ ] **Network Policies**: Restrict inter-container communication

#### Database Security
- [ ] **Strong Passwords**: Min 16 chars, auto-generated
- [ ] **Least Privilege DB Users**: App user sadece CRUD, no CREATE/DROP
- [ ] **Connection Pooling**: Max connections limited
- [ ] **Regular Backups**: Automated daily backups, offsite storage
- [ ] **Backup Encryption**: Encrypted backups (AES-256)
- [ ] **Audit Logs**: PostgreSQL log_statement='all' for sensitive operations

### 1.6 Compliance & Standards ✅

#### GDPR Compliance
- [ ] **Data Minimization**: Sadece gerekli data toplanır
- [ ] **Right to Erasure**: Soft delete, hard delete after 30 days
- [ ] **Data Portability**: Export API (JSON format)
- [ ] **Consent Management**: Explicit user consent for data processing
- [ ] **Privacy Policy**: Clear privacy policy

#### Industry Standards
- [ ] **OWASP Top 10**: All OWASP vulnerabilities addressed
- [ ] **CIS Benchmarks**: Docker, PostgreSQL, Nginx CIS compliance
- [ ] **ISO 27001 Ready**: Policies align with ISO 27001 requirements
- [ ] **SOC 2 Type II Ready**: Audit logs, access controls for SOC 2
- [ ] **PCI DSS (if applicable)**: For payment-related keys (future)

### 1.7 Code Security ✅

#### Secure Development Practices
- [ ] **Code Reviews**: All PRs reviewed for security issues
- [ ] **Static Analysis**: SonarQube, Snyk for vulnerability scanning
- [ ] **Dependency Scanning**: Dependabot/Renovate for dependency updates
- [ ] **Secrets Scanning**: git-secrets, TruffleHog to prevent secret leaks
- [ ] **Security Training**: Developers trained on OWASP, secure coding

#### CI/CD Security
- [ ] **Pipeline Security**: Secure CI/CD pipelines (GitHub Actions)
- [ ] **Build Isolation**: Isolated build environments
- [ ] **Signed Commits**: GPG-signed commits (future)
- [ ] **Artifact Signing**: Docker images signed (Docker Content Trust)

---

## 2. Key Lifecycle Policy

### 2.1 Key Creation

#### Requirements
1. **Authorization**: Sadece authenticated users (Client, Admin roles)
2. **Certificate Selection**: Geçerli, aktif bir sertifika seçilmeli
3. **Metadata**: 
   - Name (unique per user)
   - KeyType (API_KEY, DATABASE_PASSWORD, SECRET, OTHER)
   - Description (optional)
   - ExpiresAt (optional, önerilir)
   - OwnerUserId (default: current user, Admin override edebilir)

#### Encryption Process
1. **Input**: Plaintext key value (string)
2. **Certificate Validation**: 
   - Status = Active
   - NotAfter > CurrentDate
   - IsForEncryption = true
3. **Encryption**:
   - Algorithm: AES-256-GCM
   - IV: Random 96-bit (12 bytes)
   - Key Derivation: Certificate public key ile symmetric key wrap
   - Output: Ciphertext + IV + Authentication Tag (16 bytes)
4. **Storage**: 
   - EncryptedValue (BYTEA)
   - EncryptionIV (BYTEA)
   - EncryptionTag (BYTEA)
   - CertificateId (FK)
5. **Audit**: AuditTrail + RabbitMQ event published

#### Validation Rules
- Name: 1-200 chars, alphanumeric + spaces
- Value: Min 1 char, max 4KB
- KeyType: Enum (API_KEY, DATABASE_PASSWORD, SECRET, OTHER)
- ExpiresAt: Future date (if provided)

---

### 2.2 Key Retrieval (Decryption)

#### Authorization
1. **Identity Check**: JWT token validation
2. **Ownership Check**: User = Key.OwnerUserId OR User.Role = Admin
3. **Permission Check**: User has `Key.Retrieve` permission
4. **Status Check**: Key.Status = Active (not Expired, Revoked, Archived)
5. **Expiration Check**: ExpiresAt > CurrentDate (if set)

#### Decryption Process
1. **Certificate Validation**:
   - Certificate.Status = Active
   - Certificate.NotAfter > CurrentDate
2. **Decryption**:
   - Retrieve: EncryptedValue, EncryptionIV, EncryptionTag, CertificateId
   - Load Certificate Private Key (if stored) or use HSM
   - Decrypt: AES-256-GCM with IV and Tag verification
   - Output: Plaintext key value
3. **Audit Logging** (Critical):
   - KeyAccessLogs table: AccessedBy, AccessedAt, IPAddress, UserAgent, IsSuccessful
   - AuditTrails table: Action=Key.Retrieved, Severity=Info
   - RabbitMQ: Publish to `key-events` queue
4. **Metrics Update**:
   - Key.LastAccessedAt = NOW
   - Key.AccessCount++
5. **Rate Limiting**: Decrement user's retrieval quota (Redis)

#### Response
- **Success**: 
  ```json
  {
    "keyId": "uuid",
    "name": "Production DB Password",
    "value": "MyS3cr3tP@ssw0rd!",
    "expiresAt": "2026-01-01T00:00:00Z",
    "retrievedAt": "2025-10-30T12:00:00Z"
  }
  ```
- **Failure**: 
  - 403 Forbidden: Unauthorized
  - 410 Gone: Key expired/revoked
  - 500 Internal Server Error: Decryption failed

#### Security Measures
- **One-Time Display**: Frontend shows key value once, then clears
- **Copy to Clipboard**: Secure clipboard API, auto-clear after 30 sec
- **Session Recording Prevention**: Prevent screenshot/screen recording (best effort)
- **Notification**: Optional email notification on key retrieval (configurable)

---

### 2.3 Key Rotation

#### When to Rotate
1. **Scheduled Rotation**: Every 90 days (configurable per key type)
2. **Compromised Key**: Immediate rotation if suspected breach
3. **Certificate Renewal**: When encryption certificate rotated
4. **User Request**: Manual rotation via API/Portal

#### Rotation Process
1. **Authorization**: Owner or Admin only
2. **New Value Input**: User provides new plaintext value
3. **Version Increment**: Key.Version++
4. **Create New Record**:
   - New key entry with same KeyId but Version++
   - Old version: Status → Archived
5. **Encrypt New Value**: Same process as Key Creation
6. **Audit**: Log rotation event with reason
7. **Notification**: Alert dependent systems (if integrated)

#### Dual-Running Period (Optional)
- Old version active for 24 hours (grace period)
- Allows dependent systems to update
- After grace period: Old version → Status: Archived

---

### 2.4 Key Expiration

#### Automatic Expiration
1. **Background Job**: Daily cron job (2 AM UTC)
2. **Check**: `SELECT * FROM Keys WHERE ExpiresAt < NOW() AND Status = 'Active'`
3. **Update**: Status → Expired
4. **Notification**: Email to owner 7 days before expiration
5. **Audit**: Log expiration event

#### Post-Expiration
- **Retrieval**: Blocked (410 Gone)
- **Renewal**: Owner can renew (reset ExpiresAt) or rotate
- **Grace Period**: 30 days before hard archival

---

### 2.5 Key Revocation

#### Revocation Triggers
1. **Security Incident**: Compromised key
2. **User Offboarding**: Employee termination
3. **Compliance Requirement**: Policy violation
4. **Manual Request**: User/Admin revokes

#### Revocation Process
1. **Authorization**: Owner or Admin
2. **Reason Required**: Mandatory reason field (audit trail)
3. **Immediate Effect**: Status → Revoked
4. **Update Fields**:
   - RevokedAt = NOW
   - RevokedBy = CurrentUserId
   - RevokedReason = reason
5. **Audit**: Critical severity log
6. **Notification**: Email to owner + security team
7. **Dependent Systems**: Alert via RabbitMQ

#### Post-Revocation
- **No Retrieval**: Permanently blocked
- **No Rotation**: Cannot rotate revoked key
- **Archival**: After 90 days, move to cold storage

---

### 2.6 Key Deletion

#### Soft Delete (Default)
1. **Authorization**: Owner or Admin
2. **Validation**: Key.Status can be anything except Active (must revoke first)
3. **Update**: DeletedAt = NOW
4. **Query Filter**: Soft-deleted keys excluded from queries
5. **Retention**: 30 days retention period
6. **Audit**: Log deletion event

#### Hard Delete (After Retention)
1. **Background Job**: Monthly job deletes keys where DeletedAt < NOW - 30 days
2. **Secure Deletion**:
   - Overwrite EncryptedValue with random data (3 passes)
   - Delete database record
   - Purge from backups (GDPR right to erasure)
3. **Audit**: Final deletion log (then archived to cold storage)

#### Emergency Deletion
- **Compliance/Legal Requirement**: Immediate hard delete
- **Authorization**: Admin + approval workflow
- **Audit**: Critical severity, detailed justification required

---

### 2.7 Certificate Lifecycle Integration

#### Certificate Expiry Impact
1. **Certificate Expires**: 
   - All keys encrypted with that certificate become **inaccessible**
   - Status remains Active, but retrieval fails with error
2. **Certificate Renewal**:
   - Upload new certificate
   - **Key Re-Encryption**: Background job re-encrypts all keys with new certificate
   - Automated or manual (Admin approval)
3. **Certificate Revocation**:
   - All associated keys → Status: Revoked
   - Audit logs generated

#### Certificate Rotation Best Practices
1. **Overlap Period**: New certificate uploaded 30 days before old expires
2. **Gradual Migration**: Re-encrypt 10% of keys per day
3. **Rollback Plan**: Keep old certificate active during migration
4. **Notification**: Warn users 60 days before certificate expiry

---

### 2.8 Compliance & Audit Requirements

#### Regulatory Compliance
- **GDPR**: Key deletion = data erasure (right to be forgotten)
- **SOC 2**: Complete audit trail of all key operations
- **PCI DSS**: Encryption key rotation every 90 days (if applicable)
- **HIPAA**: Audit logs retained for 6 years (if applicable)

#### Audit Trail Contents (Per Key)
1. **Created**: By, At, Certificate, InitialVersion
2. **Accessed**: All retrievals (Who, When, Where, Success/Fail)
3. **Rotated**: Old→New version, Reason, By, At
4. **Revoked**: Reason, By, At
5. **Deleted**: Soft/Hard, By, At

#### Reporting
- **Monthly Report**: Key creation/retrieval/rotation statistics
- **Security Report**: Failed access attempts, suspicious patterns
- **Compliance Report**: Export for auditors (CSV/PDF)

---

### 2.9 Key Recovery & Backup

#### Backup Strategy
1. **Database Backups**: Daily PostgreSQL backups (encrypted)
2. **Certificate Backups**: Certificates stored in secure vault (encrypted)
3. **Key Export**: Admin can export key metadata (not values) for disaster recovery
4. **Offsite Storage**: Backups replicated to secondary region

#### Disaster Recovery
1. **RTO**: 4 hours (restore database + certificates)
2. **RPO**: 1 hour (incremental backups)
3. **Recovery Plan**:
   - Restore PostgreSQL from backup
   - Restore certificates from vault
   - Validate key decryption works
   - Resume operations

#### Key Recovery (User Lost Access)
- **Password Reset**: Standard password reset flow
- **MFA Recovery**: Backup codes (future feature)
- **Admin Override**: Admin can grant temporary access (logged)

---

### 2.10 Security Best Practices Summary

#### For Users
1. ✅ Set expiration dates for all keys (max 1 year)
2. ✅ Rotate keys every 90 days
3. ✅ Use descriptive names (no secrets in name)
4. ✅ Revoke keys immediately if compromised
5. ✅ Don't share keys via email/Slack (use Secure Box only)

#### For Admins
1. ✅ Monitor key access patterns (ELK dashboards)
2. ✅ Enforce key rotation policies (automated reminders)
3. ✅ Regularly audit user permissions
4. ✅ Test disaster recovery quarterly
5. ✅ Keep certificates up-to-date

#### For DevOps
1. ✅ Automate certificate renewal (Let's Encrypt)
2. ✅ Monitor certificate expiry (30-day alerts)
3. ✅ Patch systems regularly (OS, Docker, dependencies)
4. ✅ Run vulnerability scans monthly
5. ✅ Backup encryption keys to HSM (future)

---

## 3. Threat Model & Mitigations

### Threat 1: Unauthorized Key Access
**Mitigation**: JWT auth, RBAC, rate limiting, audit logging

### Threat 2: Man-in-the-Middle Attack
**Mitigation**: TLS 1.3, certificate pinning, HSTS

### Threat 3: SQL Injection
**Mitigation**: Parameterized queries (EF Core), input validation

### Threat 4: Brute Force Login
**Mitigation**: Rate limiting (5 attempts/5 min), account lockout, CAPTCHA (future)

### Threat 5: Insider Threat
**Mitigation**: Row-level security, comprehensive audit logs, least privilege

### Threat 6: Certificate Compromise
**Mitigation**: Certificate rotation, automated re-encryption, HSM storage (future)

### Threat 7: DDoS Attack
**Mitigation**: Nginx rate limiting, Cloudflare (future), auto-scaling

### Threat 8: Backup Theft
**Mitigation**: Encrypted backups, offsite storage, access controls

---

## Sonuç

Bu güvenlik policy ve key lifecycle management, Secure Box sisteminin **defense-in-depth** stratejisi ile korunmasını sağlar. Tüm kontroller düzenli olarak gözden geçirilmeli ve test edilmelidir (penetration testing, security audits).

**Next Steps**:
1. Penetration testing (quarterly)
2. Security audit (annually)
3. Compliance certification (SOC 2, ISO 27001)
4. HSM integration (production grade)

