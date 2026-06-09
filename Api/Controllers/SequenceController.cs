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
[Route("api/sequences")]
public class SequenceController(
    CargoInboxContext context,
    SequenceEngineService sequenceEngine,
    ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";

    [HttpGet]
    public async Task<IActionResult> GetSequences()
    {
        var sequences = await context.EmailSequences
            .AsNoTracking()
            .Include(s => s.Steps.OrderBy(st => st.StepOrder))
            .OrderByDescending(s => s.CreatedAt)
            .Take(50)
            .ToListAsync();
        return Ok(sequences);
    }

    [HttpPost]
    public async Task<IActionResult> CreateSequence([FromBody] EmailSequence sequence)
    {
        sequence.Id = Guid.NewGuid().ToString("N");
        sequence.TenantId = tenantProvider.TenantId;
        sequence.UserId = CurrentUserId;
        sequence.CreatedAt = DateTime.UtcNow;

        var stepOrder = 0;
        foreach (var step in sequence.Steps)
        {
            step.Id = Guid.NewGuid().ToString("N");
            step.TenantId = tenantProvider.TenantId;
            step.SequenceId = sequence.Id;
            step.StepOrder = stepOrder++;
        }

        context.Set<EmailSequence>().Add(sequence);
        await context.SaveChangesAsync();
        return Ok(sequence);
    }

    [HttpPost("{id}/start")]
    public async Task<IActionResult> StartSequence(string id, [FromQuery] string conversationId, [FromQuery] string? userId)
    {
        var sequence = await context.Set<EmailSequence>().FirstOrDefaultAsync(s => s.Id == id);
        if (sequence == null) return NotFound(new { message = "序列不存在" });
        await sequenceEngine.StartSequenceAsync(id, conversationId, userId ?? CurrentUserId);
        return Ok(new { message = "序列已启动" });
    }

    [HttpGet("{id}/executions")]
    public async Task<IActionResult> GetExecutions(string id)
    {
        var exists = await context.EmailSequences.AsNoTracking().AnyAsync(s => s.Id == id);
        if (!exists) return NotFound();

        var executions = await context.SequenceExecutions
            .AsNoTracking()
            .Where(e => e.SequenceId == id)
            .OrderByDescending(e => e.StartedAt)
            .Take(100)
            .Select(e => new
            {
                e.Id,
                e.ConversationId,
                e.UserId,
                e.CurrentStep,
                e.NextStepAt,
                e.IsCompleted,
                e.StartedAt
            })
            .ToListAsync();

        return Ok(executions);
    }

    [HttpGet("executions/active")]
    public async Task<IActionResult> GetActiveExecutions()
    {
        var executions = await context.SequenceExecutions
            .AsNoTracking()
            .Where(e => !e.IsCompleted)
            .OrderBy(e => e.NextStepAt)
            .Take(200)
            .Select(e => new
            {
                e.Id,
                e.SequenceId,
                e.ConversationId,
                e.UserId,
                e.CurrentStep,
                e.NextStepAt,
                e.StartedAt
            })
            .ToListAsync();

        return Ok(executions);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSequence(string id)
    {
        var sequence = await context.Set<EmailSequence>().FirstOrDefaultAsync(s => s.Id == id);
        if (sequence == null) return NotFound();
        context.Set<EmailSequence>().Remove(sequence);
        await context.SaveChangesAsync();
        return Ok(new { message = "序列已删除" });
    }
}
