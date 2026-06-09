using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/service/dashboard")]
public class ServiceDashboardController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var tenantId = tenantProvider.TenantId;
        var weekStart = DateTime.UtcNow.AddDays(-7);

        var openTickets = await context.ServiceTickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.New
                || t.Status == TicketStatus.Open
                || t.Status == TicketStatus.Pending)
            .ToListAsync();

        var resolvedThisWeek = await context.ServiceTickets
            .AsNoTracking()
            .Where(t => t.Status == TicketStatus.Resolved
                && t.ResolvedAt != null
                && t.ResolvedAt >= weekStart)
            .ToListAsync();

        double? avgFirstResponseMinutes = null;
        var frtSamples = resolvedThisWeek
            .Where(t => t.FirstResponseAt != null)
            .Select(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();
        if (frtSamples.Count > 0)
            avgFirstResponseMinutes = Math.Round(frtSamples.Average(), 1);

        var byChannel = openTickets
            .GroupBy(t => t.Channel)
            .Select(g => new
            {
                channel = g.Key.ToString(),
                channelCode = (int)g.Key,
                count = g.Count()
            })
            .OrderByDescending(x => x.count)
            .ToList();

        var agentWorkload = openTickets
            .Where(t => !string.IsNullOrEmpty(t.AssignedToUserId))
            .GroupBy(t => new { t.AssignedToUserId, t.AssignedToUserName })
            .Select(g => new
            {
                g.Key.AssignedToUserId,
                g.Key.AssignedToUserName,
                openCount = g.Count(),
                slaBreached = g.Count(t => t.IsSlaBreached)
            })
            .OrderByDescending(x => x.openCount)
            .Take(12)
            .ToList();

        var unassigned = openTickets.Count(t => string.IsNullOrEmpty(t.AssignedToUserId));
        var slaBreached = openTickets.Count(t => t.IsSlaBreached);

        var openConversations = await context.Conversations
            .AsNoTracking()
            .CountAsync(c => c.Status == MailStatus.Open || c.Status == MailStatus.Assigned);

        var recentTickets = await context.ServiceTickets
            .AsNoTracking()
            .OrderByDescending(t => t.UpdatedAt)
            .Take(12)
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.Subject,
                t.Status,
                t.Priority,
                t.Channel,
                t.AssignedToUserName,
                t.IsSlaBreached,
                t.ConversationId,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totals = new
            {
                openTickets = openTickets.Count,
                newTickets = openTickets.Count(t => t.Status == TicketStatus.New),
                pendingTickets = openTickets.Count(t => t.Status == TicketStatus.Pending),
                unassigned,
                slaBreached,
                openConversations,
                resolvedThisWeek = resolvedThisWeek.Count,
                avgFirstResponseMinutes
            },
            byChannel,
            agentWorkload,
            recentTickets
        });
    }
}
