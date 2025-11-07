// The build errors in this file have been fixed. Please run the build again.
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using SecureBox.Core.DTOs;
using SecureBox.Core.Entities;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Data;

namespace SecureBox.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;

    public AuthService(
        SecureBoxDbContext dbContext,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;

        var jwtSection = configuration.GetSection("JwtSettings");
        var secret = jwtSection["SecretKey"] ?? throw new InvalidOperationException("JWT SecretKey not configured");
        _issuer = jwtSection["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured");
        _audience = jwtSection["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured");
        _accessTokenExpirationMinutes = int.TryParse(jwtSection["AccessTokenExpirationMinutes"], out var accessMinutes)
            ? accessMinutes
            : 15;
        _refreshTokenExpirationDays = int.TryParse(jwtSection["RefreshTokenExpirationDays"], out var refreshDays)
            ? refreshDays
            : 7;

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Kullanıcı adı veya şifre geçersiz");
        }

        var username = request.Username.Trim();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleOrDefaultAsync(u => u.Username == username);

        if (user is null)
        {
            throw new UnauthorizedAccessException("Kullanıcı adı veya şifre geçersiz");
        }

        if (!user.IsActive)
        {
            throw new UnauthorizedAccessException("Hesabınız pasif durumda. Lütfen sistem yöneticisine başvurun.");
        }

        if (user.LockedOutUntil.HasValue && user.LockedOutUntil.Value > DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Hesabınız geçici olarak kilitlendi. Lütfen daha sonra tekrar deneyin.");
        }

        var passwordMatches = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        if (!passwordMatches)
        {
            RegisterFailedAttempt(user);
            await _dbContext.SaveChangesAsync();
            throw new UnauthorizedAccessException("Kullanıcı adı veya şifre geçersiz");
        }

        ResetFailedAttempts(user);
        user.LastLoginAt = DateTime.UtcNow;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        var roles = user.UserRoles
            .Select(ur => ur.Role?.RoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .Distinct()
            .ToList();

        var accessToken = GenerateToken(user, roles, isRefreshToken: false);
        var refreshToken = GenerateToken(user, roles, isRefreshToken: true);

        var userDto = new UserDto(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            roles,
            user.CreatedAt,
            user.LastLoginAt);

        return new AuthResponse(
            accessToken,
            refreshToken,
            _accessTokenExpirationMinutes * 60,
            "Bearer",
            userDto);
    }

    public async Task<TokenResponse> RefreshTokenAsync(string refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            throw new UnauthorizedAccessException("Geçersiz oturum yenileme isteği");
        }

        try
        {
            var principal = ValidateToken(refreshToken, expectRefreshToken: true);
            var userIdValue = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Sub)?.Value ??
                              principal.Claims.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!Guid.TryParse(userIdValue, out var userId))
            {
                throw new UnauthorizedAccessException("Kullanıcı doğrulanamadı");
            }

            var user = await _dbContext.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .SingleOrDefaultAsync(u => u.UserId == userId);

            if (user is null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("Kullanıcı bulunamadı");
            }

            var roles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role!)
                .Distinct()
                .ToList();

            var newAccessToken = GenerateToken(user, roles, isRefreshToken: false);

            return new TokenResponse(
                newAccessToken,
                _accessTokenExpirationMinutes * 60,
                "Bearer");
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Invalid refresh token received");
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş oturum yenileme isteği");
        }
    }

    public Task LogoutAsync(Guid userId, string? refreshToken = null)
    {
        // Token revocation is not persisted yet; method provided for future enhancements.
        _logger.LogInformation("User {UserId} logged out", userId);
        return Task.CompletedTask;
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId);
        if (user is null)
        {
            return false;
        }

        if (!BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash))
        {
            return false;
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return false;
        }

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return true;
    }

    private string GenerateToken(User user, IReadOnlyCollection<string> roles, bool isRefreshToken)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new("token_type", isRefreshToken ? "refresh" : "access")
        };

        if (!string.IsNullOrWhiteSpace(user.FirstName))
        {
            claims.Add(new Claim(ClaimTypes.GivenName, user.FirstName));
        }

        if (!string.IsNullOrWhiteSpace(user.LastName))
        {
            claims.Add(new Claim(ClaimTypes.Surname, user.LastName));
        }

        foreach (var role in roles)
        {
            claims.Add(new Claim(ClaimTypes.Role, role));
        }

        var expires = isRefreshToken
            ? DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
            : DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var signingCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = signingCredentials
        };

        var token = _tokenHandler.CreateToken(tokenDescriptor);
        return _tokenHandler.WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token, bool expectRefreshToken)
    {
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = _issuer,
            ValidAudience = _audience,
            IssuerSigningKey = _signingKey,
            ClockSkew = TimeSpan.Zero
        };

        var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);
        if (validatedToken is not JwtSecurityToken jwtToken ||
            !jwtToken.Header.Alg.Equals(SecurityAlgorithms.HmacSha256, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token");
        }

        var tokenType = principal.Claims.FirstOrDefault(c => c.Type == "token_type")?.Value;
        var expectedType = expectRefreshToken ? "refresh" : "access";
        if (!string.Equals(tokenType, expectedType, StringComparison.OrdinalIgnoreCase))
        {
            throw new SecurityTokenException("Invalid token type");
        }

        return principal;
    }

    private static void RegisterFailedAttempt(User user)
    {
        user.FailedLoginAttempts += 1;
        if (user.FailedLoginAttempts >= MaxFailedLoginAttempts)
        {
            user.LockedOutUntil = DateTime.UtcNow.Add(LockoutDuration);
            user.FailedLoginAttempts = 0;
        }

        user.UpdatedAt = DateTime.UtcNow;
    }

    private static void ResetFailedAttempts(User user)
    {
        user.FailedLoginAttempts = 0;
        user.LockedOutUntil = null;
    }
}

public class UserService : IUserService
{
    private readonly SecureBoxDbContext _dbContext;
    private readonly ILogger<UserService> _logger;

    public UserService(SecureBoxDbContext dbContext, ILogger<UserService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<IEnumerable<UserDto>> GetAllUsersAsync(UserQueryParams queryParams)
    {
        var query = _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(queryParams.Search))
        {
            query = query.Where(u => u.Username.Contains(queryParams.Search) || 
                                     u.Email.Contains(queryParams.Search));
        }

        if (queryParams.IsActive.HasValue)
        {
            query = query.Where(u => u.IsActive == queryParams.IsActive.Value);
        }

        var users = await query
            .Skip((queryParams.Page - 1) * queryParams.PageSize)
            .Take(queryParams.PageSize)
            .Select(u => new UserDto(
                u.UserId,
                u.Username,
                u.Email,
                u.FirstName,
                u.LastName,
                u.IsActive,
                u.UserRoles.Select(ur => ur.Role!.RoleName).ToList(),
                u.CreatedAt,
                u.LastLoginAt
            ))
            .ToListAsync();

        return users;
    }
    
    public async Task<UserDto?> GetUserByIdAsync(Guid userId)
    {
        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.UserId == userId);

        if (user == null) return null;

        return new UserDto(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            user.UserRoles.Select(ur => ur.Role!.RoleName).ToList(),
            user.CreatedAt,
            user.LastLoginAt
        );
    }
    
    public async Task<UserDto> CreateUserAsync(CreateUserRequest request)
    {
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        
        var user = new User
        {
            UserId = Guid.NewGuid(),
            Username = request.Username,
            Email = request.Email,
            PasswordHash = passwordHash,
            FirstName = request.FirstName,
            LastName = request.LastName,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync();

        return new UserDto(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            new List<string>(),
            user.CreatedAt,
            user.LastLoginAt
        );
    }
    
    public async Task<UserDto> UpdateUserAsync(Guid userId, UpdateUserRequest request)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new KeyNotFoundException("User not found");

        if (request.Email != null) user.Email = request.Email;
        if (request.FirstName != null) user.FirstName = request.FirstName;
        if (request.LastName != null) user.LastName = request.LastName;
        if (request.IsActive.HasValue) user.IsActive = request.IsActive.Value;

        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        return new UserDto(
            user.UserId,
            user.Username,
            user.Email,
            user.FirstName,
            user.LastName,
            user.IsActive,
            new List<string>(),
            user.CreatedAt,
            user.LastLoginAt
        );
    }
    
    public async Task DeleteUserAsync(Guid userId)
    {
        var user = await _dbContext.Users.FindAsync(userId);
        if (user == null) throw new KeyNotFoundException("User not found");

        user.DeletedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task AssignRoleToUserAsync(Guid userId, Guid roleId, Guid assignedBy)
    {
        var exists = await _dbContext.UserRoles
            .AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (exists) return;

        _dbContext.UserRoles.Add(new UserRole
        {
            UserRoleId = Guid.NewGuid(),
            UserId = userId,
            RoleId = roleId,
            AssignedAt = DateTime.UtcNow,
            AssignedBy = assignedBy
        });

        await _dbContext.SaveChangesAsync();
    }

    public async Task RemoveRoleFromUserAsync(Guid userId, Guid roleId)
    {
        var userRole = await _dbContext.UserRoles
            .FirstOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId);

        if (userRole != null)
        {
            _dbContext.UserRoles.Remove(userRole);
            await _dbContext.SaveChangesAsync();
        }
    }
}

public class CertificateService : ICertificateService
{
    public Task<IEnumerable<CertificateDto>> GetAllCertificatesAsync(CertificateQueryParams queryParams)
    {
        throw new NotImplementedException();
    }
    
    public Task<CertificateDto?> GetCertificateByIdAsync(Guid certificateId)
    {
        throw new NotImplementedException();
    }
    
    public Task<CertificateDto> UploadCertificateAsync(UploadCertificateRequest request, Guid uploadedBy)
    {
        throw new NotImplementedException();
    }
    
    public Task<CertificateDto> UpdateCertificateAsync(Guid certificateId, UpdateCertificateRequest request)
    {
        throw new NotImplementedException();
    }
    
    public Task RevokeCertificateAsync(Guid certificateId, string reason, Guid revokedBy)
    {
        throw new NotImplementedException();
    }
    
    public Task DeleteCertificateAsync(Guid certificateId)
    {
        throw new NotImplementedException();
    }
}

public class KeyService : IKeyService
{
    public Task<IEnumerable<KeyDto>> GetAllKeysAsync(KeyQueryParams queryParams, Guid userId, bool isAdmin)
    {
        throw new NotImplementedException();
    }
    
    public Task<KeyDto?> GetKeyByIdAsync(Guid keyId, Guid userId, bool isAdmin)
    {
        throw new NotImplementedException();
    }
    
    public Task<KeyDto> CreateKeyAsync(CreateKeyRequest request, Guid createdBy)
    {
        throw new NotImplementedException();
    }
    
    public Task<RetrieveKeyResponse> RetrieveKeyAsync(Guid keyId, Guid userId, string? reason)
    {
        throw new NotImplementedException();
    }
    
    public Task<KeyDto> UpdateKeyAsync(Guid keyId, UpdateKeyRequest request)
    {
        throw new NotImplementedException();
    }
    
    public Task<KeyDto> RotateKeyAsync(Guid keyId, string newValue, string? reason, Guid userId)
    {
        throw new NotImplementedException();
    }
    
    public Task RevokeKeyAsync(Guid keyId, string reason, Guid revokedBy)
    {
        throw new NotImplementedException();
    }
    
    public Task DeleteKeyAsync(Guid keyId)
    {
        throw new NotImplementedException();
    }
}

public class EncryptionService : IEncryptionService
{
    public Task<(byte[] encrypted, byte[] iv, byte[] tag)> EncryptAsync(string plaintext, Guid certificateId)
    {
        // TODO: Implement AES-256-GCM encryption with certificate
        throw new NotImplementedException();
    }
    
    public Task<string> DecryptAsync(byte[] ciphertext, byte[] iv, byte[] tag, Guid certificateId)
    {
        // TODO: Implement AES-256-GCM decryption with certificate
        throw new NotImplementedException();
    }
    
    public Task<bool> ValidateCertificateAsync(Guid certificateId)
    {
        throw new NotImplementedException();
    }
}

public class AuditService : IAuditService
{
    public Task LogAuditTrailAsync(AuditTrailDto auditTrail)
    {
        throw new NotImplementedException();
    }
    
    public Task<IEnumerable<AuditTrailDto>> GetAuditTrailsAsync(AuditQueryParams queryParams)
    {
        throw new NotImplementedException();
    }
    
    public Task<IEnumerable<KeyAccessLogDto>> GetKeyAccessLogsAsync(Guid keyId, Guid? userId)
    {
        throw new NotImplementedException();
    }
}