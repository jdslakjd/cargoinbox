using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class SlaTrackerService(CargoInboxContext context, TicketService ticketService)
{
    public async Task<List<Conversation>> CheckAndMarkSlaBreachesAsync()
    {
        var newlyBreached = new List<Conversation>();
        var activePolicies = await context.SlaPolicies.Where(p => p.IsActive).ToListAsync();
        if (activePolicies.Count == 0)
        {
            activePolicies.Add(new SlaPolicy { TenantId = "", FirstResponseMinutes = 120, ResolutionMinutes = 480, IsActive = true });
        }

        var openConversations = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => (c.Status == MailStatus.Open || c.Status == MailStatus.Assigned)
                && !c.IsSlaBreached)
            .ToListAsync();

        foreach (var conv in openConversations)
        {
            var policy = activePolicies.FirstOrDefault(p => p.TenantId == conv.TenantId)
                ?? activePolicies.FirstOrDefault(p => string.IsNullOrEmpty(p.TenantId))
                ?? activePolicies[0];

            var firstInbound = await context.ConversationMessages
                .Where(m => m.ConversationId == conv.Id)
                .OrderBy(m => m.DateTime)
                .FirstOrDefaultAsync();

            if (firstInbound == null) continue;

            var breached = false;

            if (conv.FirstRespondedAt == null)
            {
                var start = conv.AssignedAt ?? firstInbound.DateTime;
                if ((DateTime.UtcNow - start).TotalMinutes > policy.FirstResponseMinutes)
                    breached = true;
            }
            else if (conv.ResolvedAt == null && conv.Status != MailStatus.Resolved)
            {
                var start = firstInbound.DateTime;
                if ((DateTime.UtcNow - start).TotalMinutes > policy.ResolutionMinutes)
                    breached = true;
            }

            if (!breached) continue;

            conv.IsSlaBreached = true;
            conv.SlaBreachAt ??= DateTime.UtcNow;
            newlyBreached.Add(conv);
        }

        if (newlyBreached.Count > 0)
        {
            await context.SaveChangesAsync();
            foreach (var conv in newlyBreached)
                await ticketService.SyncSlaFromConversationAsync(conv.Id);
        }

        return newlyBreached;
    }

    public async Task MarkFirstResponseAsync(string conversationId)
    {
        var conv = await context.Conversations.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null || conv.FirstRespondedAt != null) return;
        conv.FirstRespondedAt = DateTime.UtcNow;
        conv.FirstRepliedAtUtc = DateTime.UtcNow;
        conv.SlaBreachAt = null;
        conv.IsSlaBreached = false;
        await context.SaveChangesAsync();
        await ticketService.SyncSlaFromConversationAsync(conversationId);
    }

    public async Task MarkResolvedAsync(string conversationId)
    {
        var conv = await context.Conversations.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return;
        conv.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        await ticketService.SyncSlaFromConversationAsync(conversationId);
    }
}
