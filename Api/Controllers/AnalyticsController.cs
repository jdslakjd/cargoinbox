using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics")]
public class AnalyticsController(CargoInboxContext context) : ControllerBase
{
    [HttpGet("dashboard")]
    public async Task<IActionResult> GetDashboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var conversationQuery = context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.LastMessageAt >= fromDate && c.LastMessageAt <= toDate);

        var totalInbound = await context.ConversationMessages
            .IgnoreQueryFilters()
            .CountAsync(m => m.DateTime >= fromDate && m.DateTime <= toDate);

        var resolvedCount = await conversationQuery.CountAsync(c => c.ResolvedAt != null);
        var unresolvedCount = await conversationQuery.CountAsync(c => c.ResolvedAt == null && c.Status != MailStatus.Archived);

        var resolvedConversations = await conversationQuery
            .Where(c => c.FirstRespondedAt != null)
            .Select(c => new { c.LastMessageAt, c.FirstRespondedAt })
            .ToListAsync();

        var avgFirstResponseMinutes = resolvedConversations
            .Where(c => c.FirstRespondedAt.HasValue)
            .Select(c => (c.FirstRespondedAt!.Value - c.LastMessageAt).TotalMinutes)
            .DefaultIfEmpty(0).Average();

        var breachingNow = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.SlaBreachAt != null && (c.Status == MailStatus.Open || c.Status == MailStatus.Assigned))
            .CountAsync();

        return Ok(new
        {
            Period = new { from = fromDate, to = toDate },
            TotalConversations = await conversationQuery.CountAsync(),
            TotalMessages = totalInbound,
            Resolved = resolvedCount,
            Unresolved = unresolvedCount,
            AvgFirstResponseMinutes = double.IsFinite(avgFirstResponseMinutes) ? avgFirstResponseMinutes : 0,
            SlaBreachingNow = breachingNow,
            ResolutionRate = resolvedCount + unresolvedCount > 0
                ? Math.Round((double)resolvedCount / (resolvedCount + unresolvedCount) * 100, 1) : 0
        });
    }

    [HttpGet("teammate-performance")]
    public async Task<IActionResult> GetTeammatePerformance([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var assignmentStats = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.AssignedToUserId != null && c.AssignedAt >= fromDate && c.AssignedAt <= toDate)
            .Select(c => new { c.AssignedToUserId, c.AssignedAt, c.FirstRespondedAt, c.ResolvedAt, c.SlaBreachAt })
            .ToListAsync();

        var assignments = assignmentStats
            .GroupBy(c => c.AssignedToUserId!)
            .Select(g => new
            {
                UserId = g.Key,
                TotalAssigned = g.Count(),
                Resolved = g.Count(c => c.ResolvedAt != null),
                AvgFirstResponseMinutes = g.Where(c => c.FirstRespondedAt != null)
                    .Select(c => (c.FirstRespondedAt!.Value - c.AssignedAt!.Value).TotalMinutes)
                    .DefaultIfEmpty(0).Average(),
                SlaBreaches = g.Count(c => c.SlaBreachAt != null)
            })
            .ToList();

        return Ok(assignments);
    }
}
