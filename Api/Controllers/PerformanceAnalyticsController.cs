using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/analytics/performance")]
public class PerformanceAnalyticsController(CargoInboxContext context) : ControllerBase
{
    [HttpGet("leaderboard")]
    public async Task<IActionResult> GetTeamLeaderboard([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var processedConversations = await context.Conversations
            .AsNoTracking()
            .Where(c => c.AssignedToUserId != null
                && c.AssignedAt >= fromDate && c.AssignedAt <= toDate
                && (c.FirstRepliedAtUtc != null || c.IsSlaBreached))
            .ToListAsync();

        var report = processedConversations
            .GroupBy(c => c.AssignedToUserId)
            .Select(g =>
            {
                var totalHandled = g.Count();
                var breachedCount = g.Count(c => c.IsSlaBreached);
                var respondedSessions = g.Where(c => c.FirstRepliedAtUtc.HasValue && c.AssignedAt.HasValue).ToList();
                double avgResponseMinutes = respondedSessions.Any()
                    ? respondedSessions.Average(c => (c.FirstRepliedAtUtc!.Value - c.AssignedAt!.Value).TotalMinutes)
                    : 0.0;

                return new
                {
                    UserId = g.Key,
                    TotalConversationsHandled = totalHandled,
                    AverageFirstResponseTimeMinutes = Math.Round(avgResponseMinutes, 1),
                    SlaBreachCount = breachedCount,
                    SlaComplianceRate = totalHandled > 0
                        ? Math.Round((double)(totalHandled - breachedCount) / totalHandled * 100, 1) + "%"
                        : "100%"
                };
            })
            .OrderBy(r => r.AverageFirstResponseTimeMinutes)
            .ToList();

        return Ok(report);
    }

    [HttpGet("inbox-load")]
    public async Task<IActionResult> GetInboxLoadMetrics()
    {
        var inboxMetrics = await context.Conversations
            .AsNoTracking()
            .GroupBy(c => c.SharedInboxId)
            .Select(g => new
            {
                SharedInboxId = g.Key ?? "Unassigned_Channel",
                OpenCount = g.Count(c => c.Status == MailStatus.Open),
                AssignedCount = g.Count(c => c.Status == MailStatus.Assigned),
                ArchivedCount = g.Count(c => c.Status == MailStatus.Archived),
                SnoozedCount = g.Count(c => c.Status == MailStatus.Snoozed)
            })
            .ToListAsync();

        return Ok(inboxMetrics);
    }

    [HttpGet("trends")]
    public async Task<IActionResult> GetTrends(
        [FromQuery] string granularity = "Daily",
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null)
    {
        var fromDate = from ?? DateTime.UtcNow.AddDays(-30);
        var toDate = to ?? DateTime.UtcNow;

        var messages = await context.ConversationMessages
            .IgnoreQueryFilters()
            .Where(m => m.DateTime >= fromDate && m.DateTime <= toDate)
            .ToListAsync();

        var conversations = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.LastMessageAt >= fromDate && c.LastMessageAt <= toDate)
            .ToListAsync();

        var grouped = messages
            .GroupBy(m => TruncateDate(m.DateTime, granularity))
            .OrderBy(g => g.Key)
            .Select(g => new
            {
                Date = g.Key,
                InboundCount = g.Count(),
                AssignedConversations = conversations.Count(c => c.AssignedAt?.Date == g.Key.Date && g.Key.Date <= g.Key),
                ResolvedCount = conversations.Count(c => c.ResolvedAt?.Date == g.Key.Date),
                AvgFirstResponseMinutes = conversations
                    .Where(c => c.FirstRespondedAt?.Date == g.Key.Date && c.FirstRepliedAtUtc.HasValue)
                    .Select(c => (c.FirstRepliedAtUtc!.Value - c.LastMessageAt).TotalMinutes)
                    .DefaultIfEmpty(0)
                    .Average(),
                SlaBreachCount = conversations.Count(c => c.SlaBreachAt?.Date == g.Key.Date)
            });

        return Ok(grouped);
    }

    private static DateTime TruncateDate(DateTime dt, string granularity) => granularity switch
    {
        "Weekly" => dt.Date.AddDays(-(int)dt.DayOfWeek),
        "Monthly" => new DateTime(dt.Year, dt.Month, 1),
        _ => dt.Date
    };
}
