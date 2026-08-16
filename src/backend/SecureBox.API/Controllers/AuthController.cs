using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IUserService _userService;
    private readonly IRateLimitService _rateLimit;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IUserService userService,
        IRateLimitService rateLimit,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _userService = userService;
        _rateLimit = rateLimit;
        _logger = logger;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var limit = await _rateLimit.TryAcquireAsync($"login:{ip}", 5, TimeSpan.FromMinutes(5));
        ApplyRateLimitHeaders(limit);
        if (!limit.Allowed)
        {
            return StatusCode(429, new { success = false, error = new { code = "RATE_LIMITED", message = "Too many login attempts" } });
        }

        try
        {
            var outcome = await _authService.LoginAsync(request);
            if (outcome.RequiresMfa)
            {
                return Ok(new
                {
                    success = true,
                    data = new AuthResponse(null, 0, "Bearer", null, true, outcome.MfaChallengeId),
                    message = "MFA required"
                });
            }

            return Ok(new { success = true, data = WriteSession(outcome.Session!), message = "Login successful" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed");
            return StatusCode(500, new { success = false, error = new { code = "LOGIN_ERROR", message = "An error occurred during login" } });
        }
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshTokenRequest? request = null)
    {
        try
        {
            var refresh = request?.RefreshToken
                          ?? Request.Cookies[AuthCookie.RefreshName];
            if (string.IsNullOrWhiteSpace(refresh))
            {
                return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = "Refresh token missing" } });
            }

            var session = await _authService.RefreshTokenAsync(refresh);
            return Ok(new { success = true, data = WriteSession(session) });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = ex.Message } });
        }
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromBody] LogoutRequest? request = null)
    {
        try
        {
            var userId = User.GetUserId();
            var accessToken = Request.Headers.Authorization.ToString();
            if (accessToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                accessToken = accessToken["Bearer ".Length..].Trim();
            }

            var refresh = request?.RefreshToken ?? Request.Cookies[AuthCookie.RefreshName];
            await _authService.LogoutAsync(userId, refresh, accessToken);
            AuthCookie.ClearRefresh(Response, Request);
            return Ok(new { success = true, message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            return StatusCode(500, new { success = false, error = new { code = "LOGOUT_ERROR", message = "An error occurred during logout" } });
        }
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = User.GetUserId();
            var success = await _authService.ChangePasswordAsync(userId, request);

            if (!success)
                return BadRequest(new { success = false, error = new { code = "INVALID_PASSWORD", message = "Current password is incorrect" } });

            return Ok(new { success = true, message = "Password changed successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Change password failed");
            return StatusCode(500, new { success = false, error = new { code = "PASSWORD_CHANGE_ERROR", message = "An error occurred" } });
        }
    }

    [HttpPost("mfa/verify")]
    [AllowAnonymous]
    public async Task<ActionResult> VerifyMfa([FromBody] MfaVerifyRequest request)
    {
        try
        {
            var session = await _authService.VerifyMfaAsync(request.MfaChallengeId, request.Code);
            return Ok(new { success = true, data = WriteSession(session), message = "Login successful" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_MFA", message = ex.Message } });
        }
    }

    [HttpPost("mfa/setup")]
    [Authorize]
    public async Task<ActionResult> SetupMfa()
    {
        var setup = await _authService.BeginMfaSetupAsync(User.GetUserId());
        return Ok(new { success = true, data = setup });
    }

    [HttpPost("mfa/enable")]
    [Authorize]
    public async Task<ActionResult> EnableMfa([FromBody] MfaEnableRequest request)
    {
        await _authService.EnableMfaAsync(User.GetUserId(), request.Code);
        return Ok(new { success = true, message = "MFA enabled" });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult> Me()
    {
        var user = await _userService.GetUserByIdAsync(User.GetUserId());
        if (user is null)
        {
            return Unauthorized(new { success = false, error = new { code = "UNAUTHORIZED", message = "User not found" } });
        }

        return Ok(new { success = true, data = user });
    }

    private AuthResponse WriteSession(AuthSession session)
    {
        AuthCookie.SetRefresh(Response, Request, session.RefreshToken, 7);
        return new AuthResponse(session.AccessToken, session.ExpiresIn, "Bearer", session.User);
    }

    private void ApplyRateLimitHeaders((bool Allowed, int Limit, int Remaining, TimeSpan Reset) limit)
    {
        Response.Headers["X-RateLimit-Limit"] = limit.Limit.ToString();
        Response.Headers["X-RateLimit-Remaining"] = limit.Remaining.ToString();
        Response.Headers["X-RateLimit-Reset"] = DateTimeOffset.UtcNow.Add(limit.Reset).ToUnixTimeSeconds().ToString();
    }
}

public record LogoutRequest(string? RefreshToken);
