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
[Route("api/calendar")]
public class CalendarController(
    CargoInboxContext context,
    CalendarCollisionService collisionService,
    ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Agent";

    public record BookEventRequest(
        string Title, string? Description, DateTime StartTimeUtc, DateTime EndTimeUtc,
        MeetingProvider Provider, string? RelatedContactId
    );

    [HttpPost("events")]
    public async Task<IActionResult> CreateEvent([FromBody] BookEventRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { error = "会议标题不能为空" });
        if (request.StartTimeUtc >= request.EndTimeUtc)
            return BadRequest(new { error = "会议结束时间必须大于开始时间" });

        bool isCollided = await collisionService.IsTimeSlotCollidedAsync(
            CurrentUserId, request.StartTimeUtc, request.EndTimeUtc);
        if (isCollided)
            return Conflict(new { error = "时间段冲突" });

        string joinUrl = request.Provider == MeetingProvider.Internal_LiveRoom
            ? "https://meet.cargoinbox.cn/live/" + Guid.NewGuid().ToString("N")[..8]
            : "https://zoom.us/j/" + new Random().Next(100000, 999999);

        var calendarEvent = new CalendarEvent
        {
            Title = request.Title,
            Description = request.Description,
            StartTimeUtc = request.StartTimeUtc,
            EndTimeUtc = request.EndTimeUtc,
            OrganizerUserId = CurrentUserId,
            OrganizerUserName = CurrentUserName,
            RelatedContactId = request.RelatedContactId,
            Provider = request.Provider,
            MeetingUrl = joinUrl,
            TenantId = tenantProvider.TenantId
        };
        context.Set<CalendarEvent>().Add(calendarEvent);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "CreateCalendarEvent",
            Detail = "预约会议 [" + request.Title + "]",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { success = true, eventId = calendarEvent.Id, meetingUrl = joinUrl });
    }

    [HttpGet("events")]
    public async Task<IActionResult> GetMyEvents([FromQuery] DateTime startRangeUtc, [FromQuery] DateTime endRangeUtc)
    {
        var events = await context.Set<CalendarEvent>()
            .AsNoTracking()
            .Where(e => e.OrganizerUserId == CurrentUserId
                     && !e.IsCancelled
                     && e.StartTimeUtc < endRangeUtc
                     && e.EndTimeUtc > startRangeUtc)
            .OrderBy(e => e.StartTimeUtc)
            .ToListAsync();

        return Ok(events);
    }

    [HttpDelete("events/{id}")]
    public async Task<IActionResult> CancelEvent(string id)
    {
        var evt = await context.Set<CalendarEvent>().FirstOrDefaultAsync(e => e.Id == id && e.OrganizerUserId == CurrentUserId);
        if (evt == null) return NotFound();
        evt.IsCancelled = true;
        await context.SaveChangesAsync();
        return Ok(new { message = "会议已取消" });
    }
}
