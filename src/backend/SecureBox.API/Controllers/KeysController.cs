using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class KeysController : ControllerBase
{
    private readonly IKeyService _keyService;
    private readonly ILogger<KeysController> _logger;
    
    public KeysController(IKeyService keyService, ILogger<KeysController> logger)
    {
        _keyService = keyService;
        _logger = logger;
    }
    
    /// <summary>
    /// List all accessible keys (with pagination and filters)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<KeyDto>>> GetKeys([FromQuery] KeyQueryParams queryParams)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var isAdmin = User.IsInRole("Admin");
            
            var keys = await _keyService.GetAllKeysAsync(queryParams, userId, isAdmin);
            return Ok(new { success = true, data = keys });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get keys failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_KEYS_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Get key by ID (metadata only, not the actual value)
    /// </summary>
    [HttpGet("{keyId:guid}")]
    public async Task<ActionResult<KeyDto>> GetKey(Guid keyId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var isAdmin = User.IsInRole("Admin");
            
            var key = await _keyService.GetKeyByIdAsync(keyId, userId, isAdmin);
            
            if (key == null)
                return NotFound(new { success = false, error = new { code = "KEY_NOT_FOUND", message = "Key not found" } });
            
            return Ok(new { success = true, data = key });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "GET_KEY_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Create new key (encrypts the value)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<KeyDto>> CreateKey([FromBody] CreateKeyRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var key = await _keyService.CreateKeyAsync(request, userId);
            
            return CreatedAtAction(nameof(GetKey), new { keyId = key.KeyId }, 
                new { success = true, data = key, message = "Key created and encrypted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Create key failed");
            return StatusCode(500, new { success = false, error = new { code = "CREATE_KEY_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Retrieve key value (decrypt) - CRITICAL OPERATION
    /// </summary>
    [HttpPost("{keyId:guid}/retrieve")]
    public async Task<ActionResult<RetrieveKeyResponse>> RetrieveKey(Guid keyId, [FromBody] RetrieveKeyRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var response = await _keyService.RetrieveKeyAsync(keyId, userId, request.Reason);
            
            return Ok(new 
            { 
                success = true, 
                data = response, 
                message = "Key retrieved successfully. This action has been logged." 
            });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Forbid();
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { success = false, error = new { code = "KEY_NOT_FOUND", message = ex.Message } });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Retrieve key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "RETRIEVE_KEY_ERROR", message = "Decryption failed" } });
        }
    }
    
    /// <summary>
    /// Update key metadata
    /// </summary>
    [HttpPut("{keyId:guid}")]
    public async Task<ActionResult> UpdateKey(Guid keyId, [FromBody] UpdateKeyRequest request)
    {
        try
        {
            await _keyService.UpdateKeyAsync(keyId, request);
            return Ok(new { success = true, message = "Key updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "UPDATE_KEY_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Rotate key (create new version with new value)
    /// </summary>
    [HttpPost("{keyId:guid}/rotate")]
    public async Task<ActionResult<KeyDto>> RotateKey(Guid keyId, [FromBody] RotateKeyRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var key = await _keyService.RotateKeyAsync(keyId, request.NewValue, request.Reason, userId);
            
            return Ok(new { success = true, data = key, message = "Key rotated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rotate key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "ROTATE_KEY_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Revoke key (cannot be retrieved after revocation)
    /// </summary>
    [HttpPost("{keyId:guid}/revoke")]
    public async Task<ActionResult> RevokeKey(Guid keyId, [FromBody] RevokeKeyRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            await _keyService.RevokeKeyAsync(keyId, request.Reason, userId);
            
            return Ok(new { success = true, message = "Key revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revoke key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "REVOKE_KEY_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Delete key (soft delete)
    /// </summary>
    [HttpDelete("{keyId:guid}")]
    public async Task<ActionResult> DeleteKey(Guid keyId)
    {
        try
        {
            await _keyService.DeleteKeyAsync(keyId);
            return Ok(new { success = true, message = "Key deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete key failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "DELETE_KEY_ERROR", message = "An error occurred" } });
        }
    }
}

public record RetrieveKeyRequest(string? Reason);
public record RotateKeyRequest(string NewValue, string? Reason);
public record RevokeKeyRequest(string Reason);

public class KeyNotFoundException : Exception
{
    public KeyNotFoundException(string message) : base(message) { }
}

