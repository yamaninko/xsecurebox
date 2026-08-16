using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class CertificatesController : ControllerBase
{
    private readonly ICertificateService _certificateService;

    public CertificatesController(ICertificateService certificateService)
    {
        _certificateService = certificateService;
    }

    [HttpGet]
    [Authorize(Policy = "Certificate.Read")]
    public async Task<ActionResult<IEnumerable<CertificateDto>>> GetCertificates([FromQuery] CertificateQueryParams queryParams)
    {
        var certificates = await _certificateService.GetAllCertificatesAsync(queryParams);
        return Ok(new { success = true, data = certificates });
    }

    [HttpGet("{certificateId:guid}")]
    [Authorize(Policy = "Certificate.Read")]
    public async Task<ActionResult<CertificateDto>> GetCertificate(Guid certificateId)
    {
        var certificate = await _certificateService.GetCertificateByIdAsync(certificateId);
        if (certificate == null)
            return NotFound(new { success = false, error = new { code = "CERTIFICATE_NOT_FOUND", message = "Certificate not found" } });

        return Ok(new { success = true, data = certificate });
    }

    [HttpPost]
    [Authorize(Policy = "Certificate.Create")]
    public async Task<ActionResult<CertificateDto>> UploadCertificate([FromBody] UploadCertificateRequest request)
    {
        var certificate = await _certificateService.UploadCertificateAsync(request, User.GetUserId());
        return CreatedAtAction(nameof(GetCertificate), new { certificateId = certificate.CertificateId },
            new { success = true, data = certificate, message = "Certificate uploaded successfully" });
    }

    [HttpPut("{certificateId:guid}")]
    [Authorize(Policy = "Certificate.Update")]
    public async Task<ActionResult> UpdateCertificate(Guid certificateId, [FromBody] UpdateCertificateRequest request)
    {
        await _certificateService.UpdateCertificateAsync(certificateId, request);
        return Ok(new { success = true, message = "Certificate updated successfully" });
    }

    [HttpPost("{certificateId:guid}/revoke")]
    [Authorize(Policy = "Certificate.Delete")]
    public async Task<ActionResult> RevokeCertificate(Guid certificateId, [FromBody] RevokeCertificateRequest request)
    {
        await _certificateService.RevokeCertificateAsync(certificateId, request.Reason, User.GetUserId());
        return Ok(new { success = true, message = "Certificate revoked successfully" });
    }

    [HttpDelete("{certificateId:guid}")]
    [Authorize(Policy = "Certificate.Delete")]
    public async Task<ActionResult> DeleteCertificate(Guid certificateId)
    {
        await _certificateService.DeleteCertificateAsync(certificateId);
        return Ok(new { success = true, message = "Certificate deleted successfully" });
    }
}

public record RevokeCertificateRequest(string Reason);
