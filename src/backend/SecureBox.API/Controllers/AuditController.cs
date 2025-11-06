using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;
    private readonly ILogger<AuditController> _logger;
    
    public AuditController(IAuditService auditService, ILogger<AuditController> logger)
    {
        _auditService = auditService;
        _logger = logger;
    }
    
    /// <summary>
    /// List audit trails
    /// </summary>
    [HttpGet("trails")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult<IEnumerable<AuditTrailDto>>> GetAuditTrails([FromQuery] AuditQueryParams queryParams)
    {
        try
        {
            var trails = await _auditService.GetAuditTrailsAsync(queryParams);
            return Ok(new { success = true, data = trails });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get audit trails failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_AUDIT_TRAILS_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Get key access logs for specific key
    /// </summary>
    [HttpGet("key-access/{keyId:guid}")]
    public async Task<ActionResult<IEnumerable<KeyAccessLogDto>>> GetKeyAccessLogs(Guid keyId)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var isAdmin = User.IsInRole("Admin");
            
            var logs = await _auditService.GetKeyAccessLogsAsync(keyId, isAdmin ? null : userId);
            return Ok(new { success = true, data = logs });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get key access logs failed for keyId: {KeyId}", keyId);
            return StatusCode(500, new { success = false, error = new { code = "GET_KEY_ACCESS_LOGS_ERROR", message = "An error occurred" } });
        }
    }
}

