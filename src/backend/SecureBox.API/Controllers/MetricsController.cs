using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SecureBox.API.Security;
using SecureBox.Core.Interfaces;
using SecureBox.Infrastructure.Services;

namespace SecureBox.API.Controllers;

[ApiController]
[Route("api/v1/metrics")]
[Authorize]
public class MetricsController : ControllerBase
{
    private readonly IMetricsService _metricsService;
    private readonly ILifecycleService _lifecycle;

    public MetricsController(IMetricsService metricsService, ILifecycleService lifecycle)
    {
        _metricsService = metricsService;
        _lifecycle = lifecycle;
    }

    [HttpGet]
    public async Task<ActionResult> Get()
    {
        var stats = await _metricsService.GetDashboardStatsAsync(User.GetUserId(), User.IsAdmin());
        return Ok(new { success = true, data = stats });
    }

    [HttpPost("sweep")]
    [Authorize(Roles = "Admin")]
    public async Task<ActionResult> Sweep()
    {
        var result = await _lifecycle.SweepAsync();
        return Ok(new { success = true, data = result });
    }
}
