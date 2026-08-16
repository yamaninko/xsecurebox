# Secure Box - API Endpoints

**Base URL**: `http://localhost/api` (Production: `https://securebox.local/api`)

**API Version**: v1

**Authentication**: JWT Bearer Token (except `/auth/login` and `/auth/refresh`)

---

## 1. Authentication Endpoints

### POST `/v1/auth/login`
**Description**: User login with username and password

**Auth Required**: No

**Request**:
```json
{
  "username": "admin",
  "password": "Admin@123"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGc...",
    "refreshToken": "eyJhbGc...",
    "expiresIn": 900,
    "tokenType": "Bearer",
    "user": {
      "userId": "guid",
      "username": "admin",
      "email": "admin@securebox.local",
      "firstName": "System",
      "lastName": "Administrator",
      "isActive": true,
      "roles": ["Admin"],
      "createdAt": "2025-11-07T00:00:00Z",
      "lastLoginAt": "2025-11-07T07:00:00Z"
    }
  },
  "message": "Login successful"
}
```

**Errors**:
- `401 INVALID_CREDENTIALS`: Invalid username or password
- `500 LOGIN_ERROR`: Server error

---

### POST `/v1/auth/refresh`
**Description**: Refresh access token using refresh token

**Auth Required**: No

**Request**:
```json
{
  "refreshToken": "eyJhbGc..."
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "accessToken": "eyJhbGc...",
    "expiresIn": 900,
    "tokenType": "Bearer"
  }
}
```

---

### POST `/v1/auth/change-password`
**Description**: Change user password

**Auth Required**: Yes

**Request**:
```json
{
  "currentPassword": "OldPass123!",
  "newPassword": "NewPass123!",
  "confirmPassword": "NewPass123!"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "message": "Password changed successfully"
}
```

---

## 2. User Management Endpoints

### GET `/v1/users`
**Description**: List all users (Admin only)

**Auth Required**: Yes (Admin role)

**Query Parameters**:
- `page` (int, default: 1)
- `pageSize` (int, default: 20)
- `search` (string, optional): Search in username/email
- `role` (string, optional): Filter by role name
- `isActive` (bool, optional): Filter by active status

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "userId": "guid",
      "username": "john.doe",
      "email": "john@example.com",
      "firstName": "John",
      "lastName": "Doe",
      "isActive": true,
      "roles": ["Client"],
      "createdAt": "2025-11-01T00:00:00Z",
      "lastLoginAt": "2025-11-07T06:00:00Z"
    }
  ]
}
```

---

### GET `/v1/users/{userId}`
**Description**: Get user by ID

**Auth Required**: Yes (Admin role)

**Response** (200 OK): Same as single user object above

**Errors**:
- `404 USER_NOT_FOUND`: User not found

---

### POST `/v1/users`
**Description**: Create new user

**Auth Required**: Yes (Admin role)

**Request**:
```json
{
  "username": "jane.smith",
  "email": "jane@example.com",
  "password": "SecurePass123!",
  "firstName": "Jane",
  "lastName": "Smith",
  "roleIds": ["guid-of-client-role"]
}
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": { /* user object */ },
  "message": "User created successfully"
}
```

---

### PUT `/v1/users/{userId}`
**Description**: Update user

**Auth Required**: Yes (Admin role)

**Request**:
```json
{
  "email": "newemail@example.com",
  "firstName": "Jane",
  "lastName": "Smith",
  "isActive": true
}
```

---

### DELETE `/v1/users/{userId}`
**Description**: Soft delete user

**Auth Required**: Yes (Admin role)

---

## 3. Role Management Endpoints

### GET `/v1/roles`
**Description**: List all roles

**Auth Required**: Yes (Admin role)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "roleId": "guid",
      "roleName": "Admin",
      "description": "Full system access",
      "isSystem": true,
      "userCount": 5,
      "permissionCount": 18,
      "createdAt": "2025-11-01T00:00:00Z"
    }
  ]
}
```

---

### GET `/v1/roles/{roleId}`
**Description**: Get role by ID

**Auth Required**: Yes (Admin role)

---

### POST `/v1/roles`
**Description**: Create new role

**Auth Required**: Yes (Admin role)

**Request**:
```json
{
  "roleName": "Developer",
  "description": "Development team access",
  "permissionIds": ["guid1", "guid2"]
}
```

---

### PUT `/v1/roles/{roleId}`
**Description**: Update role

**Auth Required**: Yes (Admin role)

**Request**:
```json
{
  "roleName": "Developer",
  "description": "Updated description"
}
```

**Note**: Cannot modify system roles (Admin, Client, Service)

---

### DELETE `/v1/roles/{roleId}`
**Description**: Delete role

**Auth Required**: Yes (Admin role)

**Note**: Cannot delete system roles

---

### GET `/v1/roles/permissions`
**Description**: Get all available permissions

**Auth Required**: Yes (Admin role)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "permissionId": "guid",
      "permissionName": "Key.Read",
      "resource": "Key",
      "action": "Read",
      "description": "View key metadata"
    }
  ]
}
```

---

### GET `/v1/roles/{roleId}/permissions`
**Description**: Get permissions assigned to a role

**Auth Required**: Yes (Admin role)

---

### POST `/v1/roles/{roleId}/permissions/{permissionId}`
**Description**: Assign permission to role

**Auth Required**: Yes (Admin role)

---

### DELETE `/v1/roles/{roleId}/permissions/{permissionId}`
**Description**: Remove permission from role

**Auth Required**: Yes (Admin role)

---

## 4. Key Management Endpoints

### GET `/v1/keys`
**Description**: List all accessible keys

**Auth Required**: Yes

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `status` (string): Active, Expired, Revoked, Archived
- `keyType` (string)
- `environmentTag` (string): DEV, TEST, UAT, PROD
- `tag` (string): Filter by tag
- `certificateId` (guid): Filter by certificate
- `search` (string)
- `expiringIn30Days` (bool): Show keys expiring soon

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "keyId": "guid",
      "name": "API_KEY_PRODUCTION",
      "description": "Production API key",
      "keyType": "ApiKey",
      "encryptionAlgorithm": "AES256",
      "environmentTag": "PROD",
      "tags": ["api", "production", "critical"],
      "status": "Active",
      "version": 1,
      "validFrom": "2025-11-01T00:00:00Z",
      "validTo": "2026-11-01T00:00:00Z",
      "expiresAt": "2026-11-01T00:00:00Z",
      "certificateId": "guid",
      "certificateName": "Prod Cert 2025",
      "ownerUsername": "admin",
      "createdAt": "2025-11-01T00:00:00Z",
      "lastAccessedAt": "2025-11-07T06:00:00Z",
      "accessCount": 42
    }
  ]
}
```

---

### GET `/v1/keys/{keyId}`
**Description**: Get key metadata (NOT the actual value)

**Auth Required**: Yes

---

### POST `/v1/keys`
**Description**: Create new key

**Auth Required**: Yes

**Request**:
```json
{
  "name": "DATABASE_PASSWORD",
  "description": "Production DB password",
  "keyType": "Password",
  "value": "SuperSecretPassword123!",
  "certificateId": "guid",
  "encryptionAlgorithm": "AES256",
  "environmentTag": "PROD",
  "tags": ["database", "production"],
  "validFrom": "2025-11-07T00:00:00Z",
  "validTo": "2026-11-07T00:00:00Z",
  "expiresAt": "2026-11-07T00:00:00Z"
}
```

**Response** (201 Created):
```json
{
  "success": true,
  "data": { /* key metadata */ },
  "message": "Key created successfully"
}
```

---

### POST `/v1/keys/{keyId}/retrieve`
**Description**: Retrieve (decrypt) key value - **Triple Authentication Required**

**Auth Required**: Yes + Certificate + Password

**Request**:
```json
{
  "reason": "Deploy to production server"
}
```

**Response** (200 OK):
```json
{
  "success": true,
  "data": {
    "keyId": "guid",
    "name": "DATABASE_PASSWORD",
    "value": "SuperSecretPassword123!",
    "expiresAt": "2026-11-07T00:00:00Z",
    "retrievedAt": "2025-11-07T07:00:00Z"
  }
}
```

**Note**: This action is heavily audited and logged

---

### PUT `/v1/keys/{keyId}`
**Description**: Update key metadata

**Auth Required**: Yes

**Request**:
```json
{
  "name": "DATABASE_PASSWORD_V2",
  "description": "Updated description",
  "expiresAt": "2027-11-07T00:00:00Z"
}
```

---

### POST `/v1/keys/{keyId}/rotate`
**Description**: Rotate key value (create new version)

**Auth Required**: Yes

**Request**:
```json
{
  "newValue": "NewSuperSecretPassword456!",
  "reason": "Regular rotation policy"
}
```

---

### POST `/v1/keys/{keyId}/revoke`
**Description**: Revoke key

**Auth Required**: Yes (Admin or key owner)

**Request**:
```json
{
  "reason": "Compromised key"
}
```

---

### DELETE `/v1/keys/{keyId}`
**Description**: Delete key

**Auth Required**: Yes (Admin or key owner)

---

## 5. Certificate Management Endpoints

### GET `/v1/certificates`
**Description**: List all certificates

**Auth Required**: Yes

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `status` (string): Active, Expired, Revoked
- `search` (string)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "certificateId": "guid",
      "name": "Prod Cert 2025",
      "description": "Production certificate",
      "thumbprint": "sha256hash",
      "subject": "CN=SecureBox Production",
      "issuer": "CN=SecureBox CA",
      "serialNumber": "12345",
      "algorithm": "RSA",
      "keySize": 2048,
      "notBefore": "2025-01-01T00:00:00Z",
      "notAfter": "2026-01-01T00:00:00Z",
      "status": "Active",
      "isForSigning": false,
      "isForEncryption": true,
      "uploadedBy": "admin",
      "createdAt": "2025-01-01T00:00:00Z"
    }
  ]
}
```

---

### GET `/v1/certificates/{certificateId}`
**Description**: Get certificate by ID

**Auth Required**: Yes

---

### POST `/v1/certificates/upload`
**Description**: Upload certificate

**Auth Required**: Yes (Admin role)

**Request** (multipart/form-data):
```json
{
  "name": "New Cert 2026",
  "description": "New certificate",
  "certificateFile": "<base64-encoded-cert>",
  "password": "cert-password",
  "isForSigning": false,
  "isForEncryption": true
}
```

---

### PUT `/v1/certificates/{certificateId}`
**Description**: Update certificate metadata

**Auth Required**: Yes (Admin role)

---

### POST `/v1/certificates/{certificateId}/revoke`
**Description**: Revoke certificate

**Auth Required**: Yes (Admin role)

**Request**:
```json
{
  "reason": "Certificate compromised"
}
```

---

### DELETE `/v1/certificates/{certificateId}`
**Description**: Delete certificate

**Auth Required**: Yes (Admin role)

**Note**: Cannot delete if keys are using this certificate

---

## 6. Audit Endpoints

### GET `/v1/audit/trails`
**Description**: Get audit trails

**Auth Required**: Yes (Admin role)

**Query Parameters**:
- `page` (int)
- `pageSize` (int)
- `userId` (guid, optional)
- `action` (string, optional)
- `resource` (string, optional)
- `severity` (string, optional): Low, Medium, High, Critical
- `fromDate` (datetime, optional)
- `toDate` (datetime, optional)

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "userId": "guid",
      "action": "Key.Retrieve",
      "resource": "Key",
      "resourceId": "guid",
      "details": "Retrieved key: API_KEY_PRODUCTION",
      "ipAddress": "192.168.1.10",
      "userAgent": "curl/7.68.0",
      "severity": "High"
    }
  ]
}
```

---

### GET `/v1/audit/keys/{keyId}/access-logs`
**Description**: Get key access logs

**Auth Required**: Yes

**Response** (200 OK):
```json
{
  "success": true,
  "data": [
    {
      "accessLogId": "guid",
      "keyId": "guid",
      "accessedByUsername": "john.doe",
      "accessedAt": "2025-11-07T06:00:00Z",
      "accessMethod": "API",
      "ipAddress": "192.168.1.10",
      "isSuccessful": true,
      "failureReason": null
    }
  ]
}
```

---

## Common Response Formats

### Success Response
```json
{
  "success": true,
  "data": { /* response data */ },
  "message": "Operation successful"
}
```

### Error Response
```json
{
  "success": false,
  "error": {
    "code": "ERROR_CODE",
    "message": "Human-readable error message"
  }
}
```

### Common Error Codes
- `INVALID_CREDENTIALS`: Authentication failed
- `UNAUTHORIZED`: No permission for this operation
- `NOT_FOUND`: Resource not found
- `VALIDATION_ERROR`: Request validation failed
- `CONFLICT`: Resource already exists
- `SERVER_ERROR`: Internal server error

---

## Rate Limiting

- **Default**: 100 requests/minute per IP
- **Admin Endpoints**: 50 requests/minute
- **Key Retrieval**: 10 requests/minute per key
- **Headers**: `X-RateLimit-Remaining`, `X-RateLimit-Reset`

---

**Last Updated**: 2025-11-07
