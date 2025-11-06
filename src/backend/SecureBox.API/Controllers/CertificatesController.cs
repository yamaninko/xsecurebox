using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly ICertificateService _certificateService;
    private readonly ILogger<CertificatesController> _logger;
    
    public CertificatesController(ICertificateService certificateService, ILogger<CertificatesController> logger)
    {
        _certificateService = certificateService;
        _logger = logger;
    }
    
    /// <summary>
    /// List all certificates (paginated)
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<CertificateDto>>> GetCertificates([FromQuery] CertificateQueryParams queryParams)
    {
        try
        {
            var certificates = await _certificateService.GetAllCertificatesAsync(queryParams);
            return Ok(new { success = true, data = certificates });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get certificates failed");
            return StatusCode(500, new { success = false, error = new { code = "GET_CERTIFICATES_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Get certificate by ID
    /// </summary>
    [HttpGet("{certificateId:guid}")]
    public async Task<ActionResult<CertificateDto>> GetCertificate(Guid certificateId)
    {
        try
        {
            var certificate = await _certificateService.GetCertificateByIdAsync(certificateId);
            
            if (certificate == null)
                return NotFound(new { success = false, error = new { code = "CERTIFICATE_NOT_FOUND", message = "Certificate not found" } });
            
            return Ok(new { success = true, data = certificate });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Get certificate failed for certificateId: {CertificateId}", certificateId);
            return StatusCode(500, new { success = false, error = new { code = "GET_CERTIFICATE_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Upload new certificate
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<CertificateDto>> UploadCertificate([FromBody] UploadCertificateRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            var certificate = await _certificateService.UploadCertificateAsync(request, userId);
            
            return CreatedAtAction(nameof(GetCertificate), new { certificateId = certificate.CertificateId }, 
                new { success = true, data = certificate, message = "Certificate uploaded successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Upload certificate failed");
            return StatusCode(500, new { success = false, error = new { code = "UPLOAD_CERTIFICATE_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Update certificate metadata
    /// </summary>
    [HttpPut("{certificateId:guid}")]
    public async Task<ActionResult> UpdateCertificate(Guid certificateId, [FromBody] UpdateCertificateRequest request)
    {
        try
        {
            await _certificateService.UpdateCertificateAsync(certificateId, request);
            return Ok(new { success = true, message = "Certificate updated successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update certificate failed for certificateId: {CertificateId}", certificateId);
            return StatusCode(500, new { success = false, error = new { code = "UPDATE_CERTIFICATE_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Revoke certificate
    /// </summary>
    [HttpPost("{certificateId:guid}/revoke")]
    public async Task<ActionResult> RevokeCertificate(Guid certificateId, [FromBody] RevokeCertificateRequest request)
    {
        try
        {
            var userId = Guid.Parse(User.FindFirst("sub")?.Value ?? throw new UnauthorizedAccessException());
            await _certificateService.RevokeCertificateAsync(certificateId, request.Reason, userId);
            
            return Ok(new { success = true, message = "Certificate revoked successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Revoke certificate failed for certificateId: {CertificateId}", certificateId);
            return StatusCode(500, new { success = false, error = new { code = "REVOKE_CERTIFICATE_ERROR", message = "An error occurred" } });
        }
    }
    
    /// <summary>
    /// Delete certificate (soft delete)
    /// </summary>
    [HttpDelete("{certificateId:guid}")]
    public async Task<ActionResult> DeleteCertificate(Guid certificateId)
    {
        try
        {
            await _certificateService.DeleteCertificateAsync(certificateId);
            return Ok(new { success = true, message = "Certificate deleted successfully" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Delete certificate failed for certificateId: {CertificateId}", certificateId);
            return StatusCode(500, new { success = false, error = new { code = "DELETE_CERTIFICATE_ERROR", message = "An error occurred" } });
        }
    }
}

public record RevokeCertificateRequest(string Reason);

