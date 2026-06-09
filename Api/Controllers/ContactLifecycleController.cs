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
[Route("api/contacts")]
public class ContactLifecycleController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    CrmActivityService crmActivity) : ControllerBase
{
    [HttpPut("{id}/lifecycle")]
    public async Task<IActionResult> UpdateLifecycle(string id, [FromQuery] ContactStatus status)
    {
        var contact = await context.Contacts.FindAsync(id);
        if (contact == null) return NotFound();

        var oldStatus = contact.LifecycleStatus;
        if (oldStatus == status)
            return Ok(new { success = true, currentStatus = contact.LifecycleStatus });

        contact.LifecycleStatus = status;
        contact.UpdatedAt = DateTime.UtcNow;

        if (status == ContactStatus.Converted)
        {
            var conversationIds = await context.Conversations
                .Where(c => c.ContactId == id)
                .Select(c => c.Id)
                .ToListAsync();

            var activeExecutions = await context.SequenceExecutions
                .Where(e => conversationIds.Contains(e.ConversationId) && !e.IsCompleted)
                .ToListAsync();

            foreach (var exec in activeExecutions)
            {
                exec.IsCompleted = true;
                exec.NextStepAt = null;
            }
        }

        var (userId, userName) = CrmActivityService.ResolveActor(User);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = userId ?? "system",
            UserName = userName ?? "Agent",
            Action = "ContactLifecycleTransition",
            Detail = $"客户生命周期流转: [{oldStatus}] → [{status}]",
            TenantId = tenantProvider.TenantId
        });

        await crmActivity.LogAsync(
            CrmActivityType.LifecycleChange,
            $"Lifecycle: {oldStatus} → {status}",
            contactId: id,
            companyId: contact.CompanyId,
            userId: userId,
            userName: userName);

        await context.SaveChangesAsync();
        return Ok(new { success = true, currentStatus = contact.LifecycleStatus });
    }
}
