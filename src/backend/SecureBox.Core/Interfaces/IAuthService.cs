using SecureBox.Core.DTOs;

namespace SecureBox.Core.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<TokenResponse> RefreshTokenAsync(string refreshToken);
    Task LogoutAsync(Guid userId, string? refreshToken = null);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
}

public interface IUserService
{
    Task<IEnumerable<UserDto>> GetAllUsersAsync(UserQueryParams queryParams);
    Task<UserDto?> GetUserByIdAsync(Guid userId);
    Task<UserDto> CreateUserAsync(CreateUserRequest request);
    Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request);
    Task DeleteUserAsync(Guid userId);
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
    Task<RetrieveKeyResponse> RetrieveKeyAsync(Guid keyId, Guid userId, string? reason);
    Task<KeyDto> UpdateKeyAsync(Guid keyId, UpdateKeyRequest request);
    Task<KeyDto> RotateKeyAsync(Guid keyId, string newValue, string? reason, Guid userId);
    Task RevokeKeyAsync(Guid keyId, string reason, Guid revokedBy);
    Task DeleteKeyAsync(Guid keyId);
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
    Task<IEnumerable<AuditTrailDto>> GetAuditTrailsAsync(AuditQueryParams queryParams);
    Task<IEnumerable<KeyAccessLogDto>> GetKeyAccessLogsAsync(Guid keyId, Guid? userId);
}

public interface IMessageBrokerService
{
    void PublishMessage(string queueName, object message);
    void SubscribeToQueue<T>(string queueName, Action<T> onMessage);
}

