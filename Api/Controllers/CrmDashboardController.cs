using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/dashboard")]
public class CrmDashboardController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    PipelineService pipelineService,
    CrmSegmentEvaluator segmentEvaluator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDashboard()
    {
        var pipeline = await pipelineService.EnsureDefaultPipelineAsync(tenantProvider.TenantId);

        var lifecycleCounts = await context.Contacts
            .AsNoTracking()
            .GroupBy(c => c.LifecycleStatus)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var openDeals = await context.Deals
            .AsNoTracking()
            .Where(d => d.PipelineId == pipeline.Id && d.Status == DealStatus.Open)
            .ToListAsync();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var wonThisMonth = await context.Deals.CountAsync(d =>
            d.Status == DealStatus.Won && d.ClosedAt != null && d.ClosedAt >= monthStart);
        var lostThisMonth = await context.Deals.CountAsync(d =>
            d.Status == DealStatus.Lost && d.ClosedAt != null && d.ClosedAt >= monthStart);

        var ownerStats = await context.Deals
            .AsNoTracking()
            .Where(d => d.Status == DealStatus.Open && d.OwnerUserName != null)
            .GroupBy(d => new { d.OwnerUserId, d.OwnerUserName })
            .Select(g => new
            {
                g.Key.OwnerUserId,
                g.Key.OwnerUserName,
                Count = g.Count(),
                Value = g.Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.Value)
            .Take(8)
            .ToListAsync();

        var funnel = pipeline.Stages
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.WinProbability,
                Count = openDeals.Count(d => d.StageId == s.Id),
                Value = openDeals.Where(d => d.StageId == s.Id).Sum(d => d.Amount)
            })
            .ToList();

        var segments = await context.CrmSegments.AsNoTracking().OrderByDescending(s => s.UpdatedAt).Take(10).ToListAsync();
        var segmentSummaries = new List<object>();
        foreach (var seg in segments)
        {
            segmentSummaries.Add(new
            {
                seg.Id,
                seg.Name,
                MemberCount = await segmentEvaluator.CountContactMembersAsync(context, seg)
            });
        }

        var recentActivities = await context.CrmActivities
            .AsNoTracking()
            .OrderByDescending(a => a.CreatedAt)
            .Take(15)
            .Select(a => new
            {
                a.Id,
                Type = a.Type.ToString(),
                a.Title,
                a.Body,
                a.UserName,
                a.ContactId,
                a.CreatedAt
            })
            .ToListAsync();

        return Ok(new
        {
            totals = new
            {
                contacts = await context.Contacts.CountAsync(),
                companies = await context.Companies.CountAsync(),
                openDeals = openDeals.Count,
                openDealValue = openDeals.Sum(d => d.Amount),
                wonThisMonth,
                lostThisMonth
            },
            lifecycle = lifecycleCounts.Select(x => new
            {
                status = x.Status.ToString(),
                statusCode = (int)x.Status,
                count = x.Count
            }),
            funnel,
            topOwners = ownerStats,
            segments = segmentSummaries,
            recentActivities
        });
    }
}
