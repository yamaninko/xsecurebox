using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;
    
    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }
    
    /// <summary>
    /// User login
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        try
        {
            var response = await _authService.LoginAsync(request);
            return Ok(new { success = true, data = response, message = "Login successful" });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_CREDENTIALS", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login failed for user: {Username}", request.Username);
            return StatusCode(500, new { success = false, error = new { code = "LOGIN_ERROR", message = "An error occurred during login" } });
        }
    }
    
    /// <summary>
    /// Refresh access token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<TokenResponse>> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(request.RefreshToken);
            return Ok(new { success = true, data = response });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { success = false, error = new { code = "INVALID_TOKEN", message = ex.Message } });
        }
    }
    
    /// <summary>
    /// Logout (invalidate tokens)
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult> Logout([FromBody] LogoutRequest? request = null)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            await _authService.LogoutAsync(userId, request?.RefreshToken);
            return Ok(new { success = true, message = "Logged out successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Logout failed");
            return StatusCode(500, new { success = false, error = new { code = "LOGOUT_ERROR", message = "An error occurred during logout" } });
        }
    }
    
    /// <summary>
    /// Change password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
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
}

public record RefreshTokenRequest(string RefreshToken);
public record LogoutRequest(string? RefreshToken);

