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
[Route("api/deals")]
public class DealsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    PipelineService pipelineService,
    CrmActivityService crmActivity) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDeals(
        [FromQuery] string? pipelineId,
        [FromQuery] string? stageId,
        [FromQuery] string? contactId,
        [FromQuery] DealStatus? status)
    {
        var query = context.Deals.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(pipelineId))
            query = query.Where(d => d.PipelineId == pipelineId);
        if (!string.IsNullOrEmpty(stageId))
            query = query.Where(d => d.StageId == stageId);
        if (!string.IsNullOrEmpty(contactId))
            query = query.Where(d => d.ContactId == contactId);
        if (status.HasValue)
            query = query.Where(d => d.Status == status.Value);

        var deals = await query
            .OrderByDescending(d => d.UpdatedAt)
            .Select(d => new
            {
                d.Id,
                d.Title,
                d.Amount,
                d.Currency,
                d.PipelineId,
                d.StageId,
                StageName = d.Stage!.Name,
                d.ContactId,
                ContactName = d.Contact != null ? d.Contact.Name : null,
                d.CompanyId,
                CompanyName = d.Company != null ? d.Company.Name : null,
                d.ConversationId,
                d.OwnerUserId,
                d.OwnerUserName,
                d.ExpectedCloseDate,
                d.Status,
                d.Notes,
                d.CreatedAt,
                d.UpdatedAt,
                d.ClosedAt
            })
            .ToListAsync();

        return Ok(new { data = deals });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetDeal(string id)
    {
        var deal = await context.Deals
            .AsNoTracking()
            .Include(d => d.Stage)
            .Include(d => d.Contact)
            .Include(d => d.Company)
            .FirstOrDefaultAsync(d => d.Id == id);

        if (deal == null) return NotFound();
        return Ok(deal);
    }

    [HttpPost]
    public async Task<IActionResult> CreateDeal([FromBody] DealRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
            return BadRequest(new { message = "Deal title is required" });

        var pipeline = await pipelineService.EnsureDefaultPipelineAsync(tenantProvider.TenantId);
        var stageId = request.StageId;
        if (string.IsNullOrEmpty(stageId))
        {
            stageId = pipeline.Stages.OrderBy(s => s.SortOrder).First().Id;
        }

        var stage = pipeline.Stages.FirstOrDefault(s => s.Id == stageId);
        if (stage == null)
            return BadRequest(new { message = "Invalid stage" });

        var (userId, userName) = CrmActivityService.ResolveActor(User);

        var deal = new Deal
        {
            TenantId = tenantProvider.TenantId,
            Title = request.Title.Trim(),
            Amount = request.Amount,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
            PipelineId = pipeline.Id,
            StageId = stageId,
            ContactId = request.ContactId,
            CompanyId = request.CompanyId,
            ConversationId = request.ConversationId,
            OwnerUserId = request.OwnerUserId ?? userId,
            OwnerUserName = request.OwnerUserName ?? userName,
            ExpectedCloseDate = request.ExpectedCloseDate,
            Notes = request.Notes,
            Status = DealStatus.Open
        };

        if (string.IsNullOrEmpty(deal.CompanyId) && !string.IsNullOrEmpty(deal.ContactId))
        {
            var contact = await context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == deal.ContactId);
            deal.CompanyId = contact?.CompanyId;
        }

        context.Deals.Add(deal);
        await context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deal.ContactId))
        {
            await crmActivity.LogAsync(
                CrmActivityType.DealCreated,
                $"Deal created: {deal.Title}",
                $"Amount: {deal.Currency} {deal.Amount:N2} · Stage: {stage.Name}",
                contactId: deal.ContactId,
                companyId: deal.CompanyId,
                relatedEntityId: deal.Id,
                userId: userId,
                userName: userName);
        }

        return Ok(deal);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDeal(string id, [FromBody] DealRequest request)
    {
        var deal = await context.Deals
            .Include(d => d.Stage)
            .FirstOrDefaultAsync(d => d.Id == id);
        if (deal == null) return NotFound();
        if (deal.Status != DealStatus.Open)
            return BadRequest(new { message = "Closed deals cannot be edited" });

        deal.Title = request.Title.Trim();
        deal.Amount = request.Amount;
        deal.Currency = string.IsNullOrWhiteSpace(request.Currency) ? deal.Currency : request.Currency.Trim().ToUpperInvariant();
        deal.OwnerUserId = request.OwnerUserId;
        deal.OwnerUserName = request.OwnerUserName;
        deal.ExpectedCloseDate = request.ExpectedCloseDate;
        deal.Notes = request.Notes;
        deal.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(deal);
    }

    [HttpPut("{id}/stage")]
    public async Task<IActionResult> MoveDealStage(string id, [FromBody] MoveStageRequest request)
    {
        var deal = await context.Deals.FirstOrDefaultAsync(d => d.Id == id);
        if (deal == null) return NotFound();

        var stage = await context.PipelineStages.FirstOrDefaultAsync(s => s.Id == request.StageId && s.PipelineId == deal.PipelineId);
        if (stage == null) return BadRequest(new { message = "Invalid stage" });

        var oldStage = await context.PipelineStages.AsNoTracking().FirstOrDefaultAsync(s => s.Id == deal.StageId);
        deal.StageId = stage.Id;
        deal.UpdatedAt = DateTime.UtcNow;

        if (stage.Name.Contains("Won", StringComparison.OrdinalIgnoreCase))
        {
            deal.Status = DealStatus.Won;
            deal.ClosedAt = DateTime.UtcNow;
        }
        else if (stage.Name.Contains("Lost", StringComparison.OrdinalIgnoreCase))
        {
            deal.Status = DealStatus.Lost;
            deal.ClosedAt = DateTime.UtcNow;
        }
        else
        {
            deal.Status = DealStatus.Open;
            deal.ClosedAt = null;
        }

        var (userId, userName) = CrmActivityService.ResolveActor(User);
        await context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deal.ContactId))
        {
            await crmActivity.LogAsync(
                CrmActivityType.DealStageChange,
                $"Deal moved: {oldStage?.Name ?? "?"} → {stage.Name}",
                deal.Title,
                contactId: deal.ContactId,
                companyId: deal.CompanyId,
                relatedEntityId: deal.Id,
                userId: userId,
                userName: userName);
        }

        return Ok(deal);
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseDeal(string id, [FromBody] CloseDealRequest request)
    {
        var deal = await context.Deals.FirstOrDefaultAsync(d => d.Id == id);
        if (deal == null) return NotFound();

        deal.Status = request.Won ? DealStatus.Won : DealStatus.Lost;
        deal.ClosedAt = DateTime.UtcNow;
        deal.UpdatedAt = DateTime.UtcNow;

        var targetStage = await context.PipelineStages
            .Where(s => s.PipelineId == deal.PipelineId)
            .OrderBy(s => s.SortOrder)
            .FirstOrDefaultAsync(s => s.Name.Contains(request.Won ? "Won" : "Lost", StringComparison.OrdinalIgnoreCase));

        if (targetStage != null)
            deal.StageId = targetStage.Id;

        var (userId, userName) = CrmActivityService.ResolveActor(User);
        await context.SaveChangesAsync();

        if (!string.IsNullOrEmpty(deal.ContactId))
        {
            await crmActivity.LogAsync(
                CrmActivityType.DealClosed,
                request.Won ? "Deal won" : "Deal lost",
                deal.Title,
                contactId: deal.ContactId,
                companyId: deal.CompanyId,
                relatedEntityId: deal.Id,
                userId: userId,
                userName: userName);

            if (request.Won)
            {
                var contact = await context.Contacts.FindAsync(deal.ContactId);
                if (contact != null && contact.LifecycleStatus != ContactStatus.Converted)
                {
                    contact.LifecycleStatus = ContactStatus.Converted;
                    contact.UpdatedAt = DateTime.UtcNow;
                    await context.SaveChangesAsync();
                }
            }
        }

        return Ok(deal);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetPipelineSummary([FromQuery] string? pipelineId)
    {
        var pipeline = string.IsNullOrEmpty(pipelineId)
            ? await pipelineService.EnsureDefaultPipelineAsync(tenantProvider.TenantId)
            : await context.Pipelines.Include(p => p.Stages).FirstOrDefaultAsync(p => p.Id == pipelineId);

        if (pipeline == null) return NotFound();

        var openDeals = await context.Deals
            .AsNoTracking()
            .Where(d => d.PipelineId == pipeline.Id && d.Status == DealStatus.Open)
            .ToListAsync();

        var byStage = pipeline.Stages
            .OrderBy(s => s.SortOrder)
            .Select(s => new
            {
                s.Id,
                s.Name,
                s.SortOrder,
                s.WinProbability,
                Count = openDeals.Count(d => d.StageId == s.Id),
                Value = openDeals.Where(d => d.StageId == s.Id).Sum(d => d.Amount)
            })
            .ToList();

        return Ok(new
        {
            pipelineId = pipeline.Id,
            pipelineName = pipeline.Name,
            totalOpen = openDeals.Count,
            totalValue = openDeals.Sum(d => d.Amount),
            wonCount = await context.Deals.CountAsync(d => d.PipelineId == pipeline.Id && d.Status == DealStatus.Won),
            lostCount = await context.Deals.CountAsync(d => d.PipelineId == pipeline.Id && d.Status == DealStatus.Lost),
            stages = byStage
        });
    }

    public record DealRequest(
        string Title,
        decimal Amount,
        string? Currency,
        string? StageId,
        string? ContactId,
        string? CompanyId,
        string? ConversationId,
        string? OwnerUserId,
        string? OwnerUserName,
        DateTime? ExpectedCloseDate,
        string? Notes);

    public record MoveStageRequest(string StageId);
    public record CloseDealRequest(bool Won);
}
