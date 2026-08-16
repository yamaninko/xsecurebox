using SecureBox.Core.DTOs;

namespace SecureBox.Core.Interfaces;

public interface IAuthService
{
    Task<LoginOutcome> LoginAsync(LoginRequest request);
    Task<AuthSession> VerifyMfaAsync(string challengeId, string code);
    Task<MfaSetupDto> BeginMfaSetupAsync(Guid userId);
    Task EnableMfaAsync(Guid userId, string code);
    Task<AuthSession> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(Guid userId, string? refreshToken = null, string? accessToken = null);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<bool> VerifyPasswordAsync(Guid userId, string password);
}

public record LoginOutcome(bool RequiresMfa, string? MfaChallengeId, AuthSession? Session);

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync(UserQueryParams queryParams);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task DeleteUserAsync(Guid userId);
    Task AssignRoleToUserAsync(Guid userId, Guid roleId, Guid assignedBy);
    Task RemoveRoleFromUserAsync(Guid userId, Guid roleId);
}

public interface IRoleService
{
    Task<IEnumerable<RoleDto>> GetAllRolesAsync();
    Task<RoleDto?> GetRoleByIdAsync(Guid roleId);
    Task<RoleDto> CreateRoleAsync(CreateRoleRequest request, Guid createdBy);
    Task<RoleDto> UpdateRoleAsync(Guid roleId, UpdateRoleRequest request);
    Task DeleteRoleAsync(Guid roleId);
    Task AssignPermissionToRoleAsync(Guid roleId, Guid permissionId, Guid grantedBy);
    Task RemovePermissionFromRoleAsync(Guid roleId, Guid permissionId);
    Task<IEnumerable<PermissionDto>> GetPermissionsAsync();
    Task<IEnumerable<PermissionDto>> GetRolePermissionsAsync(Guid roleId);
}

public interface ICertificateService
{
    Task<IEnumerable<CertificateDto>> GetAllCertificatesAsync(CertificateQueryParams queryParams);
    Task<CertificateDto?> GetCertificateByIdAsync(Guid certificateId);
    Task<CertificateDto> UploadCertificateAsync(UploadCertificateRequest request, Guid uploadedBy);
    Task<CertificateDto> UpdateCertificateAsync(Guid certificateId, UpdateCertificateRequest request);
    Task RevokeCertificateAsync(Guid certificateId, string reason, Guid revokedBy);
    Task DeleteCertificateAsync(Guid certificateId);
}

public interface IKeyService
{
    Task<IEnumerable<KeyDto>> GetAllKeysAsync(KeyQueryParams queryParams, Guid userId, bool isAdmin);
    Task<KeyDto?> GetKeyByIdAsync(Guid keyId, Guid userId, bool isAdmin);
    Task<KeyDto> CreateKeyAsync(CreateKeyRequest request, Guid createdBy);
    Task<RetrieveKeyResponse> RetrieveKeyAsync(Guid keyId, Guid userId, string? reason, string? password, bool passwordRequired, string? ipAddress, string? userAgent, string accessMethod = "Portal");
    Task<KeyDto> UpdateKeyAsync(Guid keyId, UpdateKeyRequest request, Guid userId, bool isAdmin);
    Task<KeyDto> RotateKeyAsync(Guid keyId, string newValue, string? reason, Guid userId);
    Task RevokeKeyAsync(Guid keyId, string reason, Guid revokedBy);
    Task DeleteKeyAsync(Guid keyId, Guid userId, bool isAdmin);
}

public interface IEncryptionService
{
    Task<(byte[] encrypted, byte[] iv, byte[] tag)> EncryptAsync(string plaintext, Guid certificateId);
    Task<string> DecryptAsync(byte[] ciphertext, byte[] iv, byte[] tag, Guid certificateId);
    Task<bool> ValidateCertificateAsync(Guid certificateId);
}

public interface IAuditService
{
    Task LogAuditTrailAsync(AuditTrailDto auditTrail);
    Task<IEnumerable<AuditTrailListDto>> GetAuditTrailsAsync(AuditQueryParams queryParams);
    Task<IEnumerable<KeyAccessLogDto>> GetKeyAccessLogsAsync(Guid keyId, Guid? userId);
}

public interface IMetricsService
{
    Task<DashboardStatsDto> GetDashboardStatsAsync(Guid userId, bool isAdmin);
}

public interface IMessageBrokerService
{
    void PublishMessage(string queueName, object message);
    void SubscribeToQueue<T>(string queueName, Action<T> onMessage);
}

