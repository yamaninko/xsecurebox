namespace SecureBox.Core.DTOs;

public record LoginRequest(string Username, string Password);

public record AuthResponse(
    string AccessToken,
    string RefreshToken,
    int ExpiresIn,
    string TokenType,
    UserDto User
);

public record TokenResponse(
    string AccessToken,
    int ExpiresIn,
    string TokenType
);

public record RefreshTokenRequest(string RefreshToken);

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
    DateTime CreatedAt,
    DateTime? LastLoginAt
);

public record CreateUserRequest(
    string Username,
    string Email,
    string Password,
    string? FirstName,
    string? LastName,
    List<Guid> RoleIds
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

