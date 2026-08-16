using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class AuditController : ControllerBase
{
    private readonly IAuditService _auditService;

    public AuditController(IAuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet("trails")]
    [Authorize(Policy = "Audit.Read")]
    public async Task<ActionResult<IEnumerable<AuditTrailListDto>>> GetAuditTrails([FromQuery] AuditQueryParams queryParams)
    {
        var trails = await _auditService.GetAuditTrailsAsync(queryParams);
        return Ok(new { success = true, data = trails });
    }

    [HttpGet("key-access/{keyId:guid}")]
    [Authorize(Policy = "Key.Read")]
    public async Task<ActionResult<IEnumerable<KeyAccessLogDto>>> GetKeyAccessLogs(Guid keyId)
    {
        var logs = await _auditService.GetKeyAccessLogsAsync(keyId, User.IsAdmin() ? null : User.GetUserId());
        return Ok(new { success = true, data = logs });
    }
}
