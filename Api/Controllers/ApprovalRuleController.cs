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
[Route("api/settings/approval")]
public class ApprovalRuleController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";

    [HttpGet]
    public async Task<IActionResult> GetRules()
    {
        var rules = await context.ApprovalRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantProvider.TenantId)
            .OrderByDescending(r => r.IsActive)
            .ThenByDescending(r => r.CreatedAt)
            .ToListAsync();
        return Ok(rules);
    }

    [HttpPost]
    public async Task<IActionResult> CreateRule([FromBody] ApprovalRule rule)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        if (string.IsNullOrWhiteSpace(rule.RequesterUserId) || string.IsNullOrWhiteSpace(rule.ApproverUserId))
            return BadRequest(new { message = "Requester and approver are required" });

        if (rule.RequesterUserId == rule.ApproverUserId)
            return BadRequest(new { message = "Requester and approver must be different users" });

        var exists = await context.ApprovalRules.AnyAsync(r =>
            r.TenantId == tenantProvider.TenantId
            && r.RequesterUserId == rule.RequesterUserId
            && r.IsActive);

        if (exists)
            return Conflict(new { message = "An active approval rule already exists for this requester" });

        rule.Id = Guid.NewGuid().ToString("N");
        rule.TenantId = tenantProvider.TenantId;
        rule.IsActive = true;
        rule.CreatedAt = DateTime.UtcNow;
        context.ApprovalRules.Add(rule);
        await context.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteRule(string id)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var rule = await context.ApprovalRules.FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantProvider.TenantId);
        if (rule == null) return NotFound();
        context.ApprovalRules.Remove(rule);
        await context.SaveChangesAsync();
        return Ok(new { message = "Approval rule deleted" });
    }
}
