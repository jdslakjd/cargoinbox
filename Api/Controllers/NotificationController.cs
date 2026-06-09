using System.Security.Claims;
using CargoInbox.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/notifications")]
public class NotificationController(NotificationService notificationService, Infrastructure.Data.CargoInboxContext context) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";

    [HttpGet]
    public async Task<IActionResult> GetNotifications([FromQuery] bool all = false)
    {
        if (all)
        {
            var allItems = await context.Notifications
                .AsNoTracking()
                .Where(n => n.UserId == CurrentUserId)
                .OrderByDescending(n => n.CreatedAt)
                .Take(50)
                .ToListAsync();
            return Ok(allItems);
        }

        var notifications = await notificationService.GetUnreadNotificationsAsync(CurrentUserId);
        return Ok(notifications);
    }

    [HttpPost("{id}/read")]
    public async Task<IActionResult> MarkAsRead(string id)
    {
        var notification = await context.Notifications.FirstOrDefaultAsync(n => n.Id == id && n.UserId == CurrentUserId);
        if (notification == null) return NotFound();
        notification.IsRead = true;
        await context.SaveChangesAsync();
        return Ok(new { message = "已标记为已读" });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllAsRead()
    {
        var unread = await context.Notifications
            .Where(n => n.UserId == CurrentUserId && !n.IsRead)
            .ToListAsync();
        foreach (var n in unread) n.IsRead = true;
        await context.SaveChangesAsync();
        return Ok(new { message = "全部已读" });
    }
}
