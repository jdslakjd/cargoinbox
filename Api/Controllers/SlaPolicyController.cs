using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings/sla")]
public class SlaPolicyController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPolicies()
    {
        var policies = await context.SlaPolicies
            .AsNoTracking()
            .OrderByDescending(p => p.IsActive)
            .ThenBy(p => p.Name)
            .ToListAsync();
        return Ok(policies);
    }

    [HttpPost]
    public async Task<IActionResult> CreatePolicy([FromBody] SlaPolicy policy)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        policy.Id = Guid.NewGuid().ToString("N");
        policy.TenantId = tenantProvider.TenantId;
        context.SlaPolicies.Add(policy);
        await context.SaveChangesAsync();
        return Ok(policy);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdatePolicy(string id, [FromBody] SlaPolicy updated)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var policy = await context.SlaPolicies.FirstOrDefaultAsync(p => p.Id == id);
        if (policy == null) return NotFound();

        policy.Name = updated.Name;
        policy.FirstResponseMinutes = updated.FirstResponseMinutes;
        policy.ResolutionMinutes = updated.ResolutionMinutes;
        policy.IsActive = updated.IsActive;

        await context.SaveChangesAsync();
        return Ok(policy);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeletePolicy(string id)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var policy = await context.SlaPolicies.FirstOrDefaultAsync(p => p.Id == id);
        if (policy == null) return NotFound();
        context.SlaPolicies.Remove(policy);
        await context.SaveChangesAsync();
        return Ok(new { message = "SLA 策略已删除" });
    }
}
