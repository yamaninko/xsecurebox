using System.Text;
using System.IdentityModel.Tokens.Jwt;
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
using SecureBox.Infrastructure.Security;

namespace SecureBox.Infrastructure.Services;

public class AuthService : IAuthService
{
    private const int MaxFailedLoginAttempts = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly SecureBoxDbContext _dbContext;
    private readonly ITokenStore _tokenStore;
    private readonly EncryptionService _encryption;
    private readonly ILogger<AuthService> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler = new();
    private readonly SymmetricSecurityKey _signingKey;
    private readonly string _issuer;
    private readonly string _audience;
    private readonly int _accessTokenExpirationMinutes;
    private readonly int _refreshTokenExpirationDays;

    public AuthService(
        SecureBoxDbContext dbContext,
        ITokenStore tokenStore,
        IEncryptionService encryption,
        IConfiguration configuration,
        ILogger<AuthService> logger)
    {
        _dbContext = dbContext;
        _tokenStore = tokenStore;
        _encryption = encryption as EncryptionService
                      ?? throw new InvalidOperationException("EncryptionService required for MFA secrets");
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

    public async Task<LoginOutcome> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new UnauthorizedAccessException("Kullanıcı adı veya şifre geçersiz");
        }

        var username = request.Username.Trim();

        var user = await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
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

        if (user.MfaEnabled)
        {
            var challengeId = $"{user.UserId:N}.{Guid.NewGuid():N}";
            await _tokenStore.StoreRefreshTokenAsync(user.UserId, "mfa:" + challengeId, TimeSpan.FromMinutes(5));
            return new LoginOutcome(true, challengeId, null);
        }

        return new LoginOutcome(false, null, await IssueSessionAsync(user));
    }

    public async Task<AuthSession> VerifyMfaAsync(string challengeId, string code)
    {
        if (string.IsNullOrWhiteSpace(challengeId) || challengeId.Length < 33)
        {
            throw new UnauthorizedAccessException("Geçersiz MFA kodu");
        }

        var userIdText = challengeId.Split('.')[0];
        if (!Guid.TryParseExact(userIdText, "N", out var userId))
        {
            throw new UnauthorizedAccessException("Geçersiz MFA kodu");
        }

        if (!await _tokenStore.RefreshTokenExistsAsync(userId, "mfa:" + challengeId))
        {
            throw new UnauthorizedAccessException("MFA oturumu süresi doldu");
        }

        var user = await LoadUserGraphAsync(userId)
                   ?? throw new UnauthorizedAccessException("Kullanıcı bulunamadı");

        var secret = Encoding.UTF8.GetString(_encryption.UnprotectPrivateKey(user.TotpSecretProtected
            ?? throw new UnauthorizedAccessException("MFA tanımlı değil")));
        if (!Totp.Verify(secret, code))
        {
            throw new UnauthorizedAccessException("Geçersiz MFA kodu");
        }

        await _tokenStore.RevokeRefreshTokenAsync(userId, "mfa:" + challengeId);
        return await IssueSessionAsync(user);
    }

    public async Task<MfaSetupDto> BeginMfaSetupAsync(Guid userId)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId)
                   ?? throw new KeyNotFoundException("User not found");
        var secret = Totp.GenerateSecret();
        user.TotpSecretProtected = _encryption.ProtectPrivateKey(Encoding.UTF8.GetBytes(secret));
        user.MfaEnabled = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
        return new MfaSetupDto(secret, Totp.OtpAuthUri("SecureBox", user.Username, secret));
    }

    public async Task EnableMfaAsync(Guid userId, string code)
    {
        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId)
                   ?? throw new KeyNotFoundException("User not found");
        if (user.TotpSecretProtected is null)
        {
            throw new InvalidOperationException("MFA kurulumu başlatılmamış");
        }

        var secret = Encoding.UTF8.GetString(_encryption.UnprotectPrivateKey(user.TotpSecretProtected));
        if (!Totp.Verify(secret, code))
        {
            throw new UnauthorizedAccessException("Geçersiz MFA kodu");
        }

        user.MfaEnabled = true;
        user.MustSetupMfa = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();
    }

    public async Task<AuthSession> RefreshTokenAsync(string refreshToken)
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
            var jti = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;

            if (!Guid.TryParse(userIdValue, out var userId) || string.IsNullOrWhiteSpace(jti))
            {
                throw new UnauthorizedAccessException("Kullanıcı doğrulanamadı");
            }

            if (!await _tokenStore.RefreshTokenExistsAsync(userId, jti))
            {
                throw new UnauthorizedAccessException("Oturum yenileme anahtarı geçersiz veya iptal edilmiş");
            }

            var user = await _dbContext.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .SingleOrDefaultAsync(u => u.UserId == userId);

            if (user is null || !user.IsActive)
            {
                throw new UnauthorizedAccessException("Kullanıcı bulunamadı");
            }

            await _tokenStore.RevokeRefreshTokenAsync(userId, jti);

            var newAccessJti = Guid.NewGuid().ToString("N");
            var newRefreshJti = Guid.NewGuid().ToString("N");
            var newAccessToken = GenerateToken(user, newAccessJti, isRefreshToken: false);
            var newRefreshToken = GenerateToken(user, newRefreshJti, isRefreshToken: true);

            await _tokenStore.StoreRefreshTokenAsync(
                user.UserId,
                newRefreshJti,
                TimeSpan.FromDays(_refreshTokenExpirationDays));

            return new AuthSession(
                newAccessToken,
                newRefreshToken,
                _accessTokenExpirationMinutes * 60,
                UserService.Map(user));
        }
        catch (SecurityTokenException ex)
        {
            _logger.LogWarning(ex, "Invalid refresh token received");
            throw new UnauthorizedAccessException("Geçersiz veya süresi dolmuş oturum yenileme isteği");
        }
    }

    public async Task LogoutAsync(Guid userId, string? refreshToken = null, string? accessToken = null)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            try
            {
                var principal = ValidateToken(refreshToken, expectRefreshToken: true);
                var jti = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrWhiteSpace(jti))
                {
                    await _tokenStore.RevokeRefreshTokenAsync(userId, jti);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Refresh token already invalid during logout");
            }
        }
        else
        {
            await _tokenStore.RevokeAllRefreshTokensAsync(userId);
        }

        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            try
            {
                var principal = ValidateToken(accessToken, expectRefreshToken: false);
                var jti = principal.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti)?.Value;
                if (!string.IsNullOrWhiteSpace(jti))
                {
                    await _tokenStore.BlacklistAccessTokenAsync(
                        jti,
                        TimeSpan.FromMinutes(_accessTokenExpirationMinutes));
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Access token already invalid during logout");
            }
        }

        _logger.LogInformation("User {UserId} logged out", userId);
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

        user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword, UserService.PasswordWorkFactor);
        user.MustChangePassword = false;
        user.UpdatedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _tokenStore.RevokeAllRefreshTokensAsync(userId);
        return true;
    }

    public async Task<bool> VerifyPasswordAsync(Guid userId, string password)
    {
        if (string.IsNullOrEmpty(password))
        {
            return false;
        }

        var user = await _dbContext.Users.SingleOrDefaultAsync(u => u.UserId == userId);
        return user is not null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash);
    }

    private async Task<AuthSession> IssueSessionAsync(User user)
    {
        var accessJti = Guid.NewGuid().ToString("N");
        var refreshJti = Guid.NewGuid().ToString("N");
        var accessToken = GenerateToken(user, accessJti, isRefreshToken: false);
        var refreshToken = GenerateToken(user, refreshJti, isRefreshToken: true);
        await _tokenStore.StoreRefreshTokenAsync(
            user.UserId,
            refreshJti,
            TimeSpan.FromDays(_refreshTokenExpirationDays));
        return new AuthSession(accessToken, refreshToken, _accessTokenExpirationMinutes * 60, UserService.Map(user));
    }

    private async Task<User?> LoadUserGraphAsync(Guid userId)
    {
        return await _dbContext.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .SingleOrDefaultAsync(u => u.UserId == userId);
    }

    private string GenerateToken(User user, string jti, bool isRefreshToken)
    {
        var roles = user.UserRoles
            .Select(ur => ur.Role?.RoleName)
            .Where(role => !string.IsNullOrWhiteSpace(role))
            .Select(role => role!)
            .Distinct()
            .ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.UniqueName, user.Username),
            new(JwtRegisteredClaimNames.Email, user.Email),
            new(JwtRegisteredClaimNames.Jti, jti),
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
            claims.Add(new Claim("role", role));
        }

        var expires = isRefreshToken
            ? DateTime.UtcNow.AddDays(_refreshTokenExpirationDays)
            : DateTime.UtcNow.AddMinutes(_accessTokenExpirationMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _issuer,
            Audience = _audience,
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256)
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
            ClockSkew = TimeSpan.Zero,
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = ClaimTypes.Role
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
