using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/schedule")]
public class ScheduleController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    [HttpPost("send")]
    public async Task<IActionResult> ScheduleSend([FromBody] ScheduledMessage message)
    {
        if (string.IsNullOrWhiteSpace(message.Subject))
            return BadRequest(new { error = "主题不能为空" });
        if (message.ScheduledAtUtc <= DateTime.UtcNow)
            return BadRequest(new { error = "定时发送时间必须在将来" });

        message.Id = Guid.NewGuid().ToString("N");
        message.TenantId = tenantProvider.TenantId;
        message.UserId = CurrentUserId;
        context.Set<ScheduledMessage>().Add(message);
        await context.SaveChangesAsync();
        return Ok(new { success = true, scheduledId = message.Id, scheduledAt = message.ScheduledAtUtc });
    }

    [HttpGet("pending")]
    public async Task<IActionResult> GetPending()
    {
        var pending = await context.Set<ScheduledMessage>()
            .AsNoTracking()
            .Where(m => m.UserId == CurrentUserId && !m.IsSent)
            .OrderBy(m => m.ScheduledAtUtc)
            .Take(50)
            .ToListAsync();
        return Ok(pending);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> CancelScheduled(string id)
    {
        var msg = await context.Set<ScheduledMessage>().FirstOrDefaultAsync(m => m.Id == id && m.UserId == CurrentUserId);
        if (msg == null) return NotFound();
        context.Set<ScheduledMessage>().Remove(msg);
        await context.SaveChangesAsync();
        return Ok(new { message = "已取消" });
    }
}
