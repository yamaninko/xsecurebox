# REST API Endpoints

## Genel Bakış

Secure Box API, RESTful prensiplere uygun olarak tasarlanmıştır. Tüm endpoint'ler JSON formatında request/response kullanır. Authentication JWT Bearer token ile sağlanır.

**Base URL**: `https://api.securebox.local/api/v1`

**Authentication**: `Authorization: Bearer <jwt_token>`

---

## Global Response Formats

### Success Response (2xx)

```json
{
  "success": true,
  "data": { /* response payload */ },
  "message": "Operation completed successfully",
  "timestamp": "2025-10-30T12:00:00Z"
}
```

### Error Response (4xx, 5xx)

```json
{
  "success": false,
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "Invalid input data",
    "details": [
      {
        "field": "email",
        "message": "Email format is invalid"
      }
    ]
  },
  "timestamp": "2025-10-30T12:00:00Z",
  "traceId": "550e8400-e29b-41d4-a716-446655440000"
}
```

### Pagination Response

```json
{
  "success": true,
  "data": [ /* items */ ],
  "pagination": {
    "page": 1,
    "pageSize": 20,
    "totalPages": 5,
    "totalCount": 100,
    "hasNext": true,
    "hasPrevious": false
  },
  "timestamp": "2025-10-30T12:00:00Z"
}
```

---

## 1. Authentication Endpoints

### 1.1 Login

**Endpoint**: `POST /auth/login`

**Description**: Kullanıcı girişi, JWT access token ve refresh token döner.

**Authorization**: None (Public)

**Request Body**:
```json
{
  "username": "string (required, 3-100 chars)",
  "password": "string (required, 8-128 chars)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "refreshToken": "550e8400-e29b-41d4-a716-446655440000",
    "expiresIn": 900,
    "tokenType": "Bearer",
    "user": {
      "userId": "uuid",
      "username": "johndoe",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "roles": ["Client"]
    }
  },
  "message": "Login successful"
}
```

**Errors**:
- `401 Unauthorized`: Invalid credentials
- `403 Forbidden`: Account locked
- `429 Too Many Requests`: Rate limit exceeded

---

### 1.2 Refresh Token

**Endpoint**: `POST /auth/refresh`

**Description**: Access token yenileme.

**Authorization**: None

**Request Body**:
```json
{
  "refreshToken": "string (required)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
    "expiresIn": 900,
    "tokenType": "Bearer"
  }
}
```

**Errors**:
- `401 Unauthorized`: Invalid or expired refresh token

---

### 1.3 Logout

**Endpoint**: `POST /auth/logout`

**Description**: Kullanıcı çıkışı, token'ı blacklist'e ekler.

**Authorization**: Required (any authenticated user)

**Request Body**:
```json
{
  "refreshToken": "string (optional)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Logged out successfully"
}
```

---

### 1.4 Change Password

**Endpoint**: `POST /auth/change-password`

**Description**: Kullanıcı kendi şifresini değiştirir.

**Authorization**: Required

**Request Body**:
```json
{
  "currentPassword": "string (required)",
  "newPassword": "string (required, min 8 chars, complexity rules)",
  "confirmPassword": "string (required, must match newPassword)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Password changed successfully"
}
```

**Errors**:
- `400 Bad Request`: Password doesn't meet complexity requirements
- `401 Unauthorized`: Current password incorrect

---

## 2. User Management Endpoints

### 2.1 List Users

**Endpoint**: `GET /users`

**Description**: Kullanıcı listesi (paginated).

**Authorization**: Required - Permission: `User.Read`

**Query Parameters**:
- `page` (int, default: 1)
- `pageSize` (int, default: 20, max: 100)
- `search` (string, optional): Username/email search
- `role` (string, optional): Filter by role
- `isActive` (bool, optional)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "userId": "uuid",
      "username": "johndoe",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "isActive": true,
      "roles": ["Client"],
      "createdAt": "2025-10-01T10:00:00Z",
      "lastLoginAt": "2025-10-30T08:00:00Z"
    }
  ],
  "pagination": { /* ... */ }
}
```

---

### 2.2 Get User by ID

**Endpoint**: `GET /users/{userId}`

**Description**: Tek kullanıcı detayı.

**Authorization**: Required - Permission: `User.Read`

**Path Parameters**:
- `userId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "userId": "uuid",
    "username": "johndoe",
    "email": "john@example.com",
    "firstName": "John",
    "lastName": "Doe",
    "isActive": true,
    "isEmailVerified": true,
    "roles": [
      {
        "roleId": "uuid",
        "roleName": "Client",
        "assignedAt": "2025-10-01T10:00:00Z"
      }
    ],
    "createdAt": "2025-10-01T10:00:00Z",
    "updatedAt": "2025-10-15T14:00:00Z",
    "lastLoginAt": "2025-10-30T08:00:00Z"
  }
}
```

**Errors**:
- `404 Not Found`: User not found

---

### 2.3 Create User

**Endpoint**: `POST /users`

**Description**: Yeni kullanıcı oluştur.

**Authorization**: Required - Permission: `User.Create`

**Request Body**:
```json
{
  "username": "string (required, 3-100 chars, unique)",
  "email": "string (required, valid email, unique)",
  "password": "string (required, min 8 chars)",
  "firstName": "string (optional, max 100 chars)",
  "lastName": "string (optional, max 100 chars)",
  "roleIds": ["uuid"] (required, at least one role)
}
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": {
    "userId": "uuid",
    "username": "newuser",
    "email": "newuser@example.com",
    "createdAt": "2025-10-30T12:00:00Z"
  },
  "message": "User created successfully"
}
```

**Errors**:
- `400 Bad Request`: Validation errors
- `409 Conflict`: Username or email already exists

---

### 2.4 Update User

**Endpoint**: `PUT /users/{userId}`

**Description**: Kullanıcı bilgilerini güncelle.

**Authorization**: Required - Permission: `User.Update` or self (own profile)

**Path Parameters**:
- `userId` (uuid, required)

**Request Body**:
```json
{
  "email": "string (optional)",
  "firstName": "string (optional)",
  "lastName": "string (optional)",
  "isActive": "boolean (optional, admin only)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "userId": "uuid",
    "username": "johndoe",
    "email": "newemail@example.com",
    "updatedAt": "2025-10-30T12:00:00Z"
  },
  "message": "User updated successfully"
}
```

---

### 2.5 Delete User

**Endpoint**: `DELETE /users/{userId}`

**Description**: Kullanıcı silme (soft delete).

**Authorization**: Required - Permission: `User.Delete`

**Path Parameters**:
- `userId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "message": "User deleted successfully"
}
```

**Errors**:
- `404 Not Found`: User not found
- `409 Conflict`: Cannot delete user with active keys

---

## 3. Role Management Endpoints

### 3.1 List Roles

**Endpoint**: `GET /roles`

**Description**: Tüm roller.

**Authorization**: Required - Permission: `Role.Read`

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "roleId": "uuid",
      "roleName": "Admin",
      "description": "Full system access",
      "isSystem": true,
      "permissions": [
        {
          "permissionId": "uuid",
          "permissionName": "User.Create",
          "resource": "User",
          "action": "Create"
        }
      ]
    }
  ]
}
```

---

### 3.2 Create Role

**Endpoint**: `POST /roles`

**Description**: Yeni rol oluştur (custom role).

**Authorization**: Required - Permission: `Role.Create`

**Request Body**:
```json
{
  "roleName": "string (required, unique)",
  "description": "string (optional)",
  "permissionIds": ["uuid"] (required, array of permission IDs)
}
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": {
    "roleId": "uuid",
    "roleName": "CustomRole",
    "createdAt": "2025-10-30T12:00:00Z"
  }
}
```

---

### 3.3 Update Role Permissions

**Endpoint**: `PUT /roles/{roleId}/permissions`

**Description**: Rol izinlerini güncelle.

**Authorization**: Required - Permission: `Role.Update`

**Path Parameters**:
- `roleId` (uuid, required)

**Request Body**:
```json
{
  "permissionIds": ["uuid"] (required, replaces all permissions)
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Role permissions updated successfully"
}
```

---

## 4. Certificate Management Endpoints

### 4.1 List Certificates

**Endpoint**: `GET /certificates`

**Description**: Sertifika listesi.

**Authorization**: Required - Permission: `Certificate.Read`

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `status` (string: Active, Expired, Revoked)
- `search` (string: name, thumbprint)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "certificateId": "uuid",
      "name": "Production Encryption Cert",
      "thumbprint": "sha256hash...",
      "subject": "CN=SecureBox Production",
      "issuer": "CN=SecureBox CA",
      "notBefore": "2025-01-01T00:00:00Z",
      "notAfter": "2026-01-01T00:00:00Z",
      "status": "Active",
      "algorithm": "RSA",
      "keySize": 2048,
      "uploadedBy": "admin",
      "createdAt": "2025-10-01T10:00:00Z"
    }
  ],
  "pagination": { /* ... */ }
}
```

---

### 4.2 Get Certificate by ID

**Endpoint**: `GET /certificates/{certificateId}`

**Description**: Sertifika detayı.

**Authorization**: Required - Permission: `Certificate.Read`

**Path Parameters**:
- `certificateId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "certificateId": "uuid",
    "name": "Production Encryption Cert",
    "description": "Main certificate for production key encryption",
    "thumbprint": "sha256hash...",
    "subject": "CN=SecureBox Production",
    "issuer": "CN=SecureBox CA",
    "serialNumber": "1234567890",
    "algorithm": "RSA",
    "keySize": 2048,
    "notBefore": "2025-01-01T00:00:00Z",
    "notAfter": "2026-01-01T00:00:00Z",
    "status": "Active",
    "isForSigning": false,
    "isForEncryption": true,
    "certificateData": "-----BEGIN CERTIFICATE-----\n...",
    "uploadedBy": {
      "userId": "uuid",
      "username": "admin"
    },
    "createdAt": "2025-10-01T10:00:00Z",
    "updatedAt": "2025-10-01T10:00:00Z"
  }
}
```

---

### 4.3 Upload Certificate

**Endpoint**: `POST /certificates`

**Description**: Sertifika yükleme (PEM veya PFX format).

**Authorization**: Required - Permission: `Certificate.Create`

**Request Body** (multipart/form-data):
```
name: string (required)
description: string (optional)
certificateFile: file (required, .pem, .cer, .pfx)
password: string (optional, for PFX)
isForSigning: boolean (default: false)
isForEncryption: boolean (default: true)
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": {
    "certificateId": "uuid",
    "name": "New Certificate",
    "thumbprint": "sha256hash...",
    "notAfter": "2026-01-01T00:00:00Z"
  },
  "message": "Certificate uploaded successfully"
}
```

**Errors**:
- `400 Bad Request`: Invalid certificate format
- `409 Conflict`: Certificate already exists (duplicate thumbprint)

---

### 4.4 Update Certificate

**Endpoint**: `PUT /certificates/{certificateId}`

**Description**: Sertifika metadata güncelleme.

**Authorization**: Required - Permission: `Certificate.Update`

**Path Parameters**:
- `certificateId` (uuid, required)

**Request Body**:
```json
{
  "name": "string (optional)",
  "description": "string (optional)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Certificate updated successfully"
}
```

---

### 4.5 Revoke Certificate

**Endpoint**: `POST /certificates/{certificateId}/revoke`

**Description**: Sertifika iptal etme.

**Authorization**: Required - Permission: `Certificate.Delete`

**Path Parameters**:
- `certificateId` (uuid, required)

**Request Body**:
```json
{
  "reason": "string (required, max 500 chars)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Certificate revoked successfully"
}
```

**Business Logic**:
- Status → "Revoked"
- Bu sertifika ile şifrelenmiş key'ler artık erişilemez (uyarı gösterilir)

---

### 4.6 Delete Certificate

**Endpoint**: `DELETE /certificates/{certificateId}`

**Description**: Sertifika silme (soft delete).

**Authorization**: Required - Permission: `Certificate.Delete`

**Path Parameters**:
- `certificateId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Certificate deleted successfully"
}
```

**Errors**:
- `409 Conflict`: Cannot delete certificate with active keys

---

## 5. Key Management Endpoints

### 5.1 List Keys

**Endpoint**: `GET /keys`

**Description**: Kullanıcının erişebileceği key listesi.

**Authorization**: Required - Permission: `Key.Read`

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `status` (string: Active, Expired, Revoked, Archived)
- `keyType` (string)
- `search` (string: name)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "keyId": "uuid",
      "name": "Production DB Password",
      "description": "Main database password",
      "keyType": "DATABASE_PASSWORD",
      "status": "Active",
      "version": 1,
      "expiresAt": "2026-01-01T00:00:00Z",
      "certificateName": "Production Encryption Cert",
      "ownerUsername": "johndoe",
      "createdAt": "2025-10-01T10:00:00Z",
      "lastAccessedAt": "2025-10-30T08:00:00Z",
      "accessCount": 150
    }
  ],
  "pagination": { /* ... */ }
}
```

**Business Logic**:
- Client users: Sadece kendi key'leri
- Admin users: Tüm key'ler

---

### 5.2 Get Key by ID (Metadata Only)

**Endpoint**: `GET /keys/{keyId}`

**Description**: Key metadata (değer değil, sadece bilgiler).

**Authorization**: Required - Permission: `Key.Read`

**Path Parameters**:
- `keyId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "keyId": "uuid",
    "name": "Production DB Password",
    "description": "Main database password",
    "keyType": "DATABASE_PASSWORD",
    "status": "Active",
    "version": 1,
    "expiresAt": "2026-01-01T00:00:00Z",
    "certificate": {
      "certificateId": "uuid",
      "name": "Production Encryption Cert",
      "thumbprint": "sha256hash..."
    },
    "owner": {
      "userId": "uuid",
      "username": "johndoe"
    },
    "createdAt": "2025-10-01T10:00:00Z",
    "createdBy": "admin",
    "updatedAt": "2025-10-15T14:00:00Z",
    "lastAccessedAt": "2025-10-30T08:00:00Z",
    "accessCount": 150
  }
}
```

---

### 5.3 Create Key

**Endpoint**: `POST /keys`

**Description**: Yeni key oluştur ve şifrele.

**Authorization**: Required - Permission: `Key.Create`

**Request Body**:
```json
{
  "name": "string (required, max 200 chars)",
  "description": "string (optional, max 1000 chars)",
  "keyType": "string (required, e.g., API_KEY, DATABASE_PASSWORD, SECRET)",
  "value": "string (required, the actual secret to encrypt)",
  "certificateId": "uuid (required, certificate to use for encryption)",
  "expiresAt": "datetime (optional)",
  "ownerUserId": "uuid (optional, defaults to current user)"
}
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": {
    "keyId": "uuid",
    "name": "Production DB Password",
    "version": 1,
    "createdAt": "2025-10-30T12:00:00Z"
  },
  "message": "Key created and encrypted successfully"
}
```

**Business Logic**:
- Value AES-256-GCM ile şifrelenir
- Certificate public key kullanılır
- IV ve Tag saklanır

---

### 5.4 Retrieve Key (Decrypted Value) ⚠️ CRITICAL

**Endpoint**: `POST /keys/{keyId}/retrieve`

**Description**: Key'in şifresi çözülerek değeri döner (audit loglanır).

**Authorization**: Required - Permission: `Key.Retrieve`

**Path Parameters**:
- `keyId` (uuid, required)

**Request Body**:
```json
{
  "reason": "string (optional, max 500 chars)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "keyId": "uuid",
    "name": "Production DB Password",
    "value": "MyS3cr3tP@ssw0rd!",
    "expiresAt": "2026-01-01T00:00:00Z",
    "retrievedAt": "2025-10-30T12:00:00Z"
  },
  "message": "Key retrieved successfully. This action has been logged."
}
```

**Business Logic**:
1. Authorization check (RBAC + ACL)
2. Certificate doğrulama (Active, not expired)
3. Decryption (AES-256-GCM)
4. Audit log (KeyAccessLogs + RabbitMQ event)
5. Update LastAccessedAt, AccessCount

**Errors**:
- `403 Forbidden`: Insufficient permissions
- `404 Not Found`: Key not found
- `410 Gone`: Key expired or revoked
- `500 Internal Server Error`: Decryption failed

---

### 5.5 Update Key

**Endpoint**: `PUT /keys/{keyId}`

**Description**: Key metadata güncelleme (value değil).

**Authorization**: Required - Permission: `Key.Update`

**Path Parameters**:
- `keyId` (uuid, required)

**Request Body**:
```json
{
  "name": "string (optional)",
  "description": "string (optional)",
  "expiresAt": "datetime (optional)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Key updated successfully"
}
```

---

### 5.6 Rotate Key (New Version)

**Endpoint**: `POST /keys/{keyId}/rotate`

**Description**: Key rotation (yeni value, version increment).

**Authorization**: Required - Permission: `Key.Update`

**Path Parameters**:
- `keyId` (uuid, required)

**Request Body**:
```json
{
  "newValue": "string (required)",
  "reason": "string (optional)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "keyId": "uuid",
    "version": 2,
    "rotatedAt": "2025-10-30T12:00:00Z"
  },
  "message": "Key rotated successfully"
}
```

**Business Logic**:
- Old version → Status: Archived
- New key record created (same KeyId, Version++)
- Audit log

---

### 5.7 Revoke Key

**Endpoint**: `POST /keys/{keyId}/revoke`

**Description**: Key iptal etme.

**Authorization**: Required - Permission: `Key.Delete`

**Path Parameters**:
- `keyId` (uuid, required)

**Request Body**:
```json
{
  "reason": "string (required, max 500 chars)"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Key revoked successfully"
}
```

**Business Logic**:
- Status → "Revoked"
- RevokedAt, RevokedBy, RevokedReason kaydedilir
- Artık retrieve edilemez

---

### 5.8 Delete Key

**Endpoint**: `DELETE /keys/{keyId}`

**Description**: Key silme (soft delete).

**Authorization**: Required - Permission: `Key.Delete`

**Path Parameters**:
- `keyId` (uuid, required)

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Key deleted successfully"
}
```

---

## 6. Audit & Logging Endpoints

### 6.1 List Audit Trails

**Endpoint**: `GET /audit/trails`

**Description**: Audit log listesi.

**Authorization**: Required - Permission: `Audit.Read`

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `userId` (uuid, optional)
- `action` (string, optional)
- `resource` (string, optional)
- `severity` (string, optional: Info, Warning, Critical)
- `fromDate` (datetime, optional)
- `toDate` (datetime, optional)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "auditId": "uuid",
      "userId": "uuid",
      "username": "johndoe",
      "action": "Key.Retrieved",
      "resource": "Key",
      "resourceId": "uuid",
      "details": {
        "keyName": "Production DB Password",
        "reason": "Deployment"
      },
      "ipAddress": "192.168.1.100",
      "timestamp": "2025-10-30T12:00:00Z",
      "severity": "Info"
    }
  ],
  "pagination": { /* ... */ }
}
```

---

### 6.2 Get Key Access Logs

**Endpoint**: `GET /audit/key-access/{keyId}`

**Description**: Belirli bir key'in tüm erişim logları.

**Authorization**: Required - Permission: `Audit.Read` or Key Owner

**Path Parameters**:
- `keyId` (uuid, required)

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `fromDate` (datetime, optional)
- `toDate` (datetime, optional)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "accessLogId": "uuid",
      "keyId": "uuid",
      "accessedBy": "johndoe",
      "accessedAt": "2025-10-30T08:00:00Z",
      "accessMethod": "API",
      "ipAddress": "192.168.1.100",
      "userAgent": "PostmanRuntime/7.28.4",
      "isSuccessful": true,
      "failureReason": null
    }
  ],
  "pagination": { /* ... */ }
}
```

---

## 7. Health & Monitoring Endpoints

### 7.1 Health Check

**Endpoint**: `GET /health`

**Description**: API health status.

**Authorization**: None (Public)

**Response** (200 OK):
```json
{
  "status": "Healthy",
  "timestamp": "2025-10-30T12:00:00Z",
  "version": "1.0.0",
  "checks": {
    "database": "Healthy",
    "redis": "Healthy",
    "mongodb": "Healthy",
    "rabbitmq": "Healthy"
  }
}
```

---

### 7.2 Metrics

**Endpoint**: `GET /metrics`

**Description**: Prometheus format metrics (or custom JSON).

**Authorization**: Required - Admin only

**Response** (200 OK):
```
# TYPE api_requests_total counter
api_requests_total{method="GET",endpoint="/keys",status="200"} 1234
# TYPE key_retrievals_total counter
key_retrievals_total 5678
# TYPE active_users_total gauge
active_users_total 42
```

---

## 8. Rate Limiting

Tüm endpoint'ler için rate limiting uygulanır (Redis-based):

**Global Limits**:
- **Anonymous**: 10 req/min
- **Authenticated Users**: 100 req/min
- **Admin**: 500 req/min

**Critical Endpoint Limits** (Key Retrieval):
- **Client**: 10 retrievals/hour
- **Service**: 100 retrievals/hour
- **Admin**: Unlimited

**Headers** (RateLimit standardı):
```
X-RateLimit-Limit: 100
X-RateLimit-Remaining: 85
X-RateLimit-Reset: 1698675600
```

**Response** (429 Too Many Requests):
```json
{
  "success": false,
  "error": {
    "code": "RATE_LIMIT_EXCEEDED",
    "message": "Too many requests. Please try again later.",
    "retryAfter": 60
  }
}
```

---

## 9. API Versioning

**Versioning Strategy**: URI Versioning

- Current: `/api/v1/*`
- Future: `/api/v2/*`

**Deprecation Policy**:
- Old versions supported for 6 months after new version release
- Deprecation warnings in response headers:
```
X-API-Deprecation: true
X-API-Sunset: 2026-04-30T00:00:00Z
X-API-Upgrade-To: /api/v2/keys
```

---

## 10. Security Headers

Tüm response'larda security headers:

```
Strict-Transport-Security: max-age=31536000; includeSubDomains
X-Content-Type-Options: nosniff
X-Frame-Options: DENY
X-XSS-Protection: 1; mode=block
Content-Security-Policy: default-src 'self'
X-Request-ID: 550e8400-e29b-41d4-a716-446655440000
```

---

## 11. CORS Policy

**Allowed Origins**: Configured in appsettings.json

```json
{
  "AllowedOrigins": [
    "https://portal.securebox.local",
    "https://admin.securebox.local"
  ],
  "AllowedMethods": ["GET", "POST", "PUT", "DELETE"],
  "AllowedHeaders": ["Authorization", "Content-Type"],
  "AllowCredentials": true
}
```

---

## 12. API Documentation

**Swagger/OpenAPI**: `https://api.securebox.local/swagger`

- Interactive API documentation
- Schema definitions
- Example requests/responses
- Authentication test UI

---

## Endpoint Özeti

| Module        | Endpoint Count | Authentication | Rate Limited |
|---------------|----------------|----------------|--------------|
| Auth          | 4              | Mixed          | ✅ Strict    |
| Users         | 5              | Required       | ✅ Moderate  |
| Roles         | 3              | Required       | ✅ Moderate  |
| Certificates  | 6              | Required       | ✅ Moderate  |
| Keys          | 8              | Required       | ✅ Critical  |
| Audit         | 2              | Required       | ✅ Moderate  |
| Health        | 2              | Mixed          | ❌ None      |
| **TOTAL**     | **30**         | -              | -            |

---

## Sonuç

Bu API tasarımı:
- ✅ RESTful prensiplere uygun
- ✅ Comprehensive error handling
- ✅ Pagination support
- ✅ Strong authentication/authorization
- ✅ Rate limiting
- ✅ Audit logging
- ✅ Security-first approach
- ✅ Developer-friendly documentation

