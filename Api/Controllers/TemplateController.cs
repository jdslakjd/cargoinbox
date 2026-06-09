using System.Security.Claims;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/templates")]
public class TemplateController(CargoInboxContext context) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";

    [HttpGet]
    public async Task<IActionResult> GetTemplates([FromQuery] string? teamId)
    {
        var query = context.MessageTemplates.AsNoTracking().AsQueryable();
        query = query.Where(t => t.UserId == CurrentUserId || t.IsShared);
        if (!string.IsNullOrEmpty(teamId)) query = query.Where(t => t.TeamId == teamId);
        var results = await query.OrderByDescending(t => t.UsageCount).Take(100).ToListAsync();
        return Ok(results);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTemplate([FromBody] MessageTemplate template)
    {
        template.Id = Guid.NewGuid().ToString("N");
        template.UserId = CurrentUserId;
        context.MessageTemplates.Add(template);
        await context.SaveChangesAsync();
        return Ok(template);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTemplate(string id, [FromBody] MessageTemplate updated)
    {
        var template = await context.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (template == null) return NotFound();
        template.Title = updated.Title;
        template.Subject = updated.Subject;
        template.Body = updated.Body;
        template.IsShared = updated.IsShared;
        template.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(template);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTemplate(string id)
    {
        var template = await context.MessageTemplates.FirstOrDefaultAsync(t => t.Id == id && t.UserId == CurrentUserId);
        if (template == null) return NotFound();
        context.MessageTemplates.Remove(template);
        await context.SaveChangesAsync();
        return Ok(new { message = "模板已删除" });
    }
}
