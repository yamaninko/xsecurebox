namespace SecureBox.Core.DTOs;

public record KeyDto(
    Guid KeyId,
    string Name,
    string? Description,
    string KeyType,
    string Status,
    int Version,
    DateTime? ExpiresAt,
    string CertificateName,
    string OwnerUsername,
    DateTime CreatedAt,
    DateTime? LastAccessedAt,
    long AccessCount
);

public record CreateKeyRequest(
    string Name,
    string? Description,
    string KeyType,
    string Value,
    Guid CertificateId,
    DateTime? ExpiresAt = null,
    Guid? OwnerUserId = null
);

public record UpdateKeyRequest(
    string? Name,
    string? Description,
    DateTime? ExpiresAt
);

public record RetrieveKeyResponse(
    Guid KeyId,
    string Name,
    string Value,
    DateTime? ExpiresAt,
    DateTime RetrievedAt
);

public record KeyQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Status = null,
    string? KeyType = null,
    string? Search = null
);

public record RetrieveKeyRequest(string? Reason = null);

public record RotateKeyRequest(
    string NewValue,
    string? Reason = null
);

public record RevokeKeyRequest(string Reason);

public record AuditTrailDto(
    Guid? UserId,
    string Action,
    string Resource,
    Guid? ResourceId,
    string? Details,
    string? IPAddress,
    string? UserAgent,
    string Severity
);

public record KeyAccessLogDto(
    Guid AccessLogId,
    Guid KeyId,
    string AccessedByUsername,
    DateTime AccessedAt,
    string AccessMethod,
    string? IPAddress,
    bool IsSuccessful,
    string? FailureReason
);

public record AuditQueryParams(
    int Page = 1,
    int PageSize = 20,
    Guid? UserId = null,
    string? Action = null,
    string? Resource = null,
    string? Severity = null,
    DateTime? FromDate = null,
    DateTime? ToDate = null
);

