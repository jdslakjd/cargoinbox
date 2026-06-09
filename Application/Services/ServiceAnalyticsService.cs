using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class ServiceAnalyticsService(CargoInboxContext context)
{
    public async Task<ServiceReportsSnapshot> BuildReportsAsync(string tenantId, int days = 30)
    {
        days = Math.Clamp(days, 7, 90);
        var periodStart = DateTime.UtcNow.Date.AddDays(-days + 1);
        var periodEnd = DateTime.UtcNow;

        var ticketsInPeriod = await context.ServiceTickets
            .AsNoTracking()
            .Where(t =>
                t.CreatedAt >= periodStart
                || (t.ResolvedAt != null && t.ResolvedAt >= periodStart)
                || t.UpdatedAt >= periodStart)
            .ToListAsync();

        var resolvedInPeriod = ticketsInPeriod
            .Where(t => t.Status is TicketStatus.Resolved or TicketStatus.Closed
                && t.ResolvedAt >= periodStart)
            .ToList();

        var dailyTrend = Enumerable.Range(0, days)
            .Select(offset =>
            {
                var day = periodStart.AddDays(offset);
                var next = day.AddDays(1);
                var created = ticketsInPeriod.Count(t => t.CreatedAt >= day && t.CreatedAt < next);
                var resolved = ticketsInPeriod.Count(t =>
                    t.ResolvedAt != null && t.ResolvedAt >= day && t.ResolvedAt < next);
                var breached = ticketsInPeriod.Count(t =>
                    t.IsSlaBreached && t.CreatedAt >= day && t.CreatedAt < next);

                var dayResolved = ticketsInPeriod
                    .Where(t => t.ResolvedAt >= day && t.ResolvedAt < next && t.FirstResponseAt != null)
                    .ToList();
                double? avgFrt = dayResolved.Count > 0
                    ? Math.Round(dayResolved.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes), 1)
                    : null;

                return new DailyTrendPoint(day.ToString("yyyy-MM-dd"), created, resolved, breached, avgFrt);
            })
            .ToList();

        var agentStats = ticketsInPeriod
            .Where(t => !string.IsNullOrEmpty(t.AssignedToUserId))
            .GroupBy(t => new { t.AssignedToUserId, t.AssignedToUserName })
            .Select(g =>
            {
                var resolved = g.Count(t => t.ResolvedAt >= periodStart);
                var withFrt = g.Where(t => t.FirstResponseAt != null && t.ResolvedAt >= periodStart).ToList();
                var withArt = g.Where(t => t.ResolvedAt >= periodStart).ToList();
                return new AgentReportRow(
                    g.Key.AssignedToUserId!,
                    g.Key.AssignedToUserName,
                    g.Count(t => t.Status is TicketStatus.New or TicketStatus.Open or TicketStatus.Pending),
                    resolved,
                    withFrt.Count > 0
                        ? Math.Round(withFrt.Average(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes), 1)
                        : null,
                    withArt.Count > 0
                        ? Math.Round(withArt.Average(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes), 1)
                        : null,
                    g.Count(t => t.IsSlaBreached));
            })
            .OrderByDescending(a => a.ResolvedCount)
            .Take(20)
            .ToList();

        var channelVolume = ticketsInPeriod
            .Where(t => t.CreatedAt >= periodStart)
            .GroupBy(t => t.Channel)
            .Select(g => new ChannelVolumeRow(g.Key.ToString(), (int)g.Key, g.Count()))
            .OrderByDescending(c => c.Count)
            .ToList();

        var totalCreated = ticketsInPeriod.Count(t => t.CreatedAt >= periodStart);
        var totalResolved = resolvedInPeriod.Count;
        var slaDenominator = ticketsInPeriod.Count(t => t.CreatedAt >= periodStart);
        var slaBreached = ticketsInPeriod.Count(t => t.IsSlaBreached && t.CreatedAt >= periodStart);
        var slaCompliance = slaDenominator > 0
            ? Math.Round(100.0 * (slaDenominator - slaBreached) / slaDenominator, 1)
            : 100.0;

        var frtAll = resolvedInPeriod
            .Where(t => t.FirstResponseAt != null)
            .Select(t => (t.FirstResponseAt!.Value - t.CreatedAt).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();
        var artAll = resolvedInPeriod
            .Select(t => (t.ResolvedAt!.Value - t.CreatedAt).TotalMinutes)
            .Where(m => m >= 0)
            .ToList();

        return new ServiceReportsSnapshot(
            periodStart,
            periodEnd,
            days,
            new ServiceReportTotals(
                totalCreated,
                totalResolved,
                slaBreached,
                slaCompliance,
                frtAll.Count > 0 ? Math.Round(frtAll.Average(), 1) : null,
                artAll.Count > 0 ? Math.Round(artAll.Average(), 1) : null),
            dailyTrend,
            agentStats,
            channelVolume);
    }

    public record ServiceReportsSnapshot(
        DateTime PeriodStart,
        DateTime PeriodEnd,
        int Days,
        ServiceReportTotals Totals,
        IReadOnlyList<DailyTrendPoint> DailyTrend,
        IReadOnlyList<AgentReportRow> AgentStats,
        IReadOnlyList<ChannelVolumeRow> ChannelVolume);

    public record ServiceReportTotals(
        int Created,
        int Resolved,
        int SlaBreached,
        double SlaCompliancePercent,
        double? AvgFirstResponseMinutes,
        double? AvgResolutionMinutes);

    public record DailyTrendPoint(
        string Date,
        int Created,
        int Resolved,
        int SlaBreached,
        double? AvgFirstResponseMinutes);

    public record AgentReportRow(
        string UserId,
        string? UserName,
        int OpenCount,
        int ResolvedCount,
        double? AvgFirstResponseMinutes,
        double? AvgResolutionMinutes,
        int SlaBreachedCount);

    public record ChannelVolumeRow(string Channel, int ChannelCode, int Count);
}
