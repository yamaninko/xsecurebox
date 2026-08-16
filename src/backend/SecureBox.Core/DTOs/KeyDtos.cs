namespace SecureBox.Core.DTOs;

public record KeyDto(
    Guid KeyId,
    string Name,
    string? Description,
    string KeyType,
    string EncryptionAlgorithm,
    string EnvironmentTag,
    List<string>? Tags,
    string Status,
    int Version,
    DateTime ValidFrom,
    DateTime? ValidTo,
    DateTime? ExpiresAt,
    Guid CertificateId,
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
    string EncryptionAlgorithm = "AES256", // RSA, AES256, ECC
    string EnvironmentTag = "DEV", // DEV, TEST, UAT, PROD
    List<string>? Tags = null,
    DateTime? ValidFrom = null,
    DateTime? ValidTo = null,
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
    string? EnvironmentTag = null,
    string? Tag = null,
    Guid? CertificateId = null,
    string? Search = null,
    bool? ExpiringIn30Days = null
);

public record RetrieveKeyRequest(string? Password = null, string? Reason = null);

public record ExpiryWarningDto(string Kind, Guid Id, string Name, DateTime ExpiresAt, int DaysLeft);

public record DashboardStatsDto(
    int TotalKeys,
    int ActiveKeys,
    int ExpiredKeys,
    int RevokedKeys,
    int TotalCertificates,
    int TotalUsers,
    IReadOnlyList<EnvironmentCountDto> KeysByEnvironment,
    IReadOnlyList<RecentActivityDto> RecentActivity,
    int ExpiringKeys30Days,
    int ExpiringCertificates30Days,
    IReadOnlyList<ExpiryWarningDto> UpcomingExpiries
);

public record EnvironmentCountDto(string Environment, int Count);

public record RecentActivityDto(string Action, string Resource, DateTime Timestamp, string Username);

public record AuditTrailListDto(
    Guid AuditId,
    Guid? UserId,
    string Username,
    string Action,
    string Resource,
    Guid? ResourceId,
    string? Details,
    string? IPAddress,
    string? UserAgent,
    string Severity,
    DateTime Timestamp
);

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

