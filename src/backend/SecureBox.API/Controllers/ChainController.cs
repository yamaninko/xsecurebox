using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.Core.DTOs;
using SecureBox.Core.Interfaces;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/chain")]
[Authorize(Roles = "Admin")]
public class ChainController : ControllerBase
{
    private readonly IChainVerificationService _chain;

    public ChainController(IChainVerificationService chain)
    {
        _chain = chain;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var data = await _chain.GetDashboardAsync();
        return Ok(new { success = true, data });
    }

    [HttpPut("settings")]
    public async Task<ActionResult> Update([FromBody] ChainSettingsRequest request)
    {
        var data = await _chain.UpdateSettingsAsync(request);
        return Ok(new { success = true, data, message = "Ayarlar kaydedildi" });
    }

    [HttpPost("redeploy")]
    public async Task<ActionResult> Redeploy([FromBody] ChainSettingsRequest? request = null)
    {
        var data = await _chain.RedeployAsync(request?.SystemName);
        return Ok(new { success = true, data, message = "Kontrat yeniden yayınlandı" });
    }

    [HttpPost("cluster")]
    public async Task<ActionResult> Scale([FromBody] ChainScaleRequest request)
    {
        var data = await _chain.ScaleClusterAsync(request.NodeCount);
        return Ok(new { success = true, data, message = $"{data.RunningNodeCount} ETH VM çalışıyor" });
    }
}
