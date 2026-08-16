namespace SecureBox.Core.DTOs;

public record LoginRequest(string Username, string Password);

public record AuthSession(string AccessToken, string RefreshToken, int ExpiresIn, UserDto User);

public record AuthResponse(
    string? AccessToken,
    int ExpiresIn,
    string TokenType,
    UserDto? User,
    bool RequiresMfa = false,
    string? MfaChallengeId = null
);

public record TokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType
);

public record MfaVerifyRequest(string MfaChallengeId, string Code);

public record MfaEnableRequest(string Code);

public record MfaSetupDto(string Secret, string OtpAuthUri);

public record RefreshTokenRequest(string? RefreshToken = null);

public record ChangePasswordRequest(
    string CurrentPassword,
    string NewPassword,
    string ConfirmPassword
);

public record UserDto(
    Guid UserId,
    string Username,
    string Email,
    string? FirstName,
    string? LastName,
    bool IsActive,
    IEnumerable<string> Roles,
    IEnumerable<string> Permissions,
    bool MustChangePassword,
    bool MfaEnabled,
    bool MustSetupMfa,
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    List<Guid>? RoleIds = null
);

public record UpdateUserRequest(
    string? Email,
    string? FirstName,
    string? LastName,
    bool? IsActive
);

public record UserQueryParams(
    int Page = 1,
    int PageSize = 20,
    string? Search = null,
    string? Role = null,
    bool? IsActive = null
);

public record RoleDto(
    Guid RoleId,
    string RoleName,
    string? Description,
    bool IsSystem,
    int UserCount,
    int PermissionCount,
    DateTime CreatedAt
);

public record CreateRoleRequest(
    string RoleName,
    string? Description,
    List<Guid>? PermissionIds = null
);

public record UpdateRoleRequest(
    string? RoleName,
    string? Description
);

public record PermissionDto(
    Guid PermissionId,
    string PermissionName,
    string Resource,
    string Action,
    string? Description
);

