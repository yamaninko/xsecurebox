using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class KeysController : ControllerBase
{
    private readonly IKeyService _keyService;
    private readonly IRateLimitService _rateLimit;
    private readonly ILogger<KeysController> _logger;

    public KeysController(IKeyService keyService, IRateLimitService rateLimit, ILogger<KeysController> logger)
    {
        _keyService = keyService;
        _rateLimit = rateLimit;
        _logger = logger;
    }

    [HttpGet]
    [Authorize(Policy = "Key.Read")]
    public async Task<ActionResult<IEnumerable<KeyDto>>> GetKeys([FromQuery] KeyQueryParams queryParams)
    {
        if (!User.HasScope("keys:read"))
            return StatusCode(403, new { success = false, error = new { code = "INSUFFICIENT_SCOPE", message = "keys:read required" } });

        var keys = await _keyService.GetAllKeysAsync(queryParams, User.GetUserId(), User.IsAdmin() || User.IsApiClient());
        return Ok(new { success = true, data = keys });
    }

    [HttpGet("{keyId:guid}")]
    [Authorize(Policy = "Key.Read")]
    public async Task<ActionResult<KeyDto>> GetKey(Guid keyId)
    {
        if (!User.HasScope("keys:read"))
            return StatusCode(403, new { success = false, error = new { code = "INSUFFICIENT_SCOPE", message = "keys:read required" } });

        var key = await _keyService.GetKeyByIdAsync(keyId, User.GetUserId(), User.IsAdmin() || User.IsApiClient());
        if (key == null)
            return NotFound(new { success = false, error = new { code = "KEY_NOT_FOUND", message = "Key not found" } });

        return Ok(new { success = true, data = key });
    }

    [HttpPost]
    [Authorize(Policy = "Key.Create")]
    public async Task<ActionResult<KeyDto>> CreateKey([FromBody] CreateKeyRequest request)
    {
        if (!User.HasScope("keys:write"))
            return StatusCode(403, new { success = false, error = new { code = "INSUFFICIENT_SCOPE", message = "keys:write required" } });

        var key = await _keyService.CreateKeyAsync(request, User.GetUserId());
        return CreatedAtAction(nameof(GetKey), new { keyId = key.KeyId },
            new { success = true, data = key, message = "Key created and encrypted successfully" });
    }

    [HttpPost("{keyId:guid}/retrieve")]
    [Authorize(Policy = "Key.Retrieve")]
    public async Task<ActionResult<RetrieveKeyResponse>> RetrieveKey(Guid keyId, [FromBody] RetrieveKeyRequest request)
    {
        var userId = User.GetUserId();
        var limitKey = User.IsAdmin() ? $"retrieve:admin:{userId}" : $"retrieve:{userId}";
        var limitCount = User.IsAdmin() ? 100 : 10;
        var limit = await _rateLimit.TryAcquireAsync(limitKey, limitCount, TimeSpan.FromHours(1));
        Response.Headers["X-RateLimit-Limit"] = limit.Limit.ToString();
        Response.Headers["X-RateLimit-Remaining"] = limit.Remaining.ToString();
        if (!limit.Allowed)
        {
            return StatusCode(429, new { success = false, error = new { code = "RATE_LIMITED", message = "Key retrieval rate limit exceeded" } });
        }

        if (User.IsApiClient() && !User.HasScope("keys:read", "keys:retrieve"))
        {
            return StatusCode(403, new { success = false, error = new { code = "INSUFFICIENT_SCOPE", message = "keys:read or keys:retrieve required" } });
        }

        var passwordRequired = !User.IsApiClient();
        var response = await _keyService.RetrieveKeyAsync(
            keyId,
            userId,
            request.Reason,
            request.Password,
            passwordRequired,
            HttpContext.Connection.RemoteIpAddress?.ToString(),
            Request.Headers.UserAgent.ToString(),
            User.IsApiClient() ? "ApiClient" : "Portal");

        return Ok(new
        {
            success = true,
            data = response,
            message = "Key retrieved successfully. This action has been logged."
        });
    }

    [HttpPut("{keyId:guid}")]
    [Authorize(Policy = "Key.Update")]
    public async Task<ActionResult> UpdateKey(Guid keyId, [FromBody] UpdateKeyRequest request)
    {
        await _keyService.UpdateKeyAsync(keyId, request, User.GetUserId(), User.IsAdmin());
        return Ok(new { success = true, message = "Key updated successfully" });
    }

    [HttpPost("{keyId:guid}/rotate")]
    [Authorize(Policy = "Key.Update")]
    public async Task<ActionResult<KeyDto>> RotateKey(Guid keyId, [FromBody] RotateKeyRequest request)
    {
        var key = await _keyService.RotateKeyAsync(keyId, request.NewValue, request.Reason, User.GetUserId());
        return Ok(new { success = true, data = key, message = "Key rotated successfully" });
    }

    [HttpPost("{keyId:guid}/revoke")]
    [Authorize(Policy = "Key.Delete")]
    public async Task<ActionResult> RevokeKey(Guid keyId, [FromBody] RevokeKeyRequest request)
    {
        await _keyService.RevokeKeyAsync(keyId, request.Reason, User.GetUserId());
        return Ok(new { success = true, message = "Key revoked successfully" });
    }

    [HttpDelete("{keyId:guid}")]
    [Authorize(Policy = "Key.Delete")]
    public async Task<ActionResult> DeleteKey(Guid keyId)
    {
        await _keyService.DeleteKeyAsync(keyId, User.GetUserId(), User.IsAdmin());
        return Ok(new { success = true, message = "Key deleted successfully" });
    }
}
