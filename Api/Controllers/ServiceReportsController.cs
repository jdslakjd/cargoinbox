using CargoInbox.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/service/reports")]
public class ServiceReportsController(
    ServiceAnalyticsService analytics,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetReports([FromQuery] int days = 30)
    {
        var snapshot = await analytics.BuildReportsAsync(tenantProvider.TenantId, days);
        return Ok(new
        {
            period = new
            {
                start = snapshot.PeriodStart,
                end = snapshot.PeriodEnd,
                days = snapshot.Days
            },
            totals = snapshot.Totals,
            dailyTrend = snapshot.DailyTrend,
            agentStats = snapshot.AgentStats,
            channelVolume = snapshot.ChannelVolume
        });
    }
}
