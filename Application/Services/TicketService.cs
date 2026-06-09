using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class TicketService(CargoInboxContext context, RoundRobinAssignmentService roundRobin)
{
    private static readonly TicketStatus[] OpenTicketStatuses =
        [TicketStatus.New, TicketStatus.Open, TicketStatus.Pending];

    public async Task<ServiceTicket> EnsureForConversationAsync(
        Conversation conversation,
        bool tryAutoAssign = true,
        CancellationToken cancellationToken = default)
    {
        var ticket = await context.ServiceTickets
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(t => t.ConversationId == conversation.Id, cancellationToken);

        if (ticket == null)
        {
            ticket = new ServiceTicket
            {
                TenantId = conversation.TenantId,
                Number = await NextTicketNumberAsync(conversation.TenantId, cancellationToken),
                ConversationId = conversation.Id,
                Subject = conversation.Title,
                Channel = conversation.Channel,
                ContactId = conversation.ContactId,
                SharedInboxId = conversation.SharedInboxId,
                Status = TicketStatus.New,
                Tags = conversation.Labels.ToList()
            };
            context.ServiceTickets.Add(ticket);
        }

        SyncFromConversation(ticket, conversation);

        if (tryAutoAssign
            && string.IsNullOrEmpty(conversation.AssignedToUserId)
            && string.IsNullOrEmpty(ticket.AssignedToUserId))
        {
            await roundRobin.TryAssignConversationAsync(conversation, sharedInboxId: conversation.SharedInboxId, cancellationToken: cancellationToken);
            SyncFromConversation(ticket, conversation);
            if (!string.IsNullOrEmpty(conversation.AssignedToUserId))
                ticket.Status = TicketStatus.Open;
        }

        ticket.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<ServiceTicket?> SyncFromConversationIdAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation == null) return null;

        return await EnsureForConversationAsync(conversation, tryAutoAssign: false, cancellationToken: cancellationToken);
    }

    public async Task<ServiceTicket?> AssignAsync(
        string ticketId,
        string userId,
        string? userName,
        CancellationToken cancellationToken = default)
    {
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket == null) return null;

        ticket.AssignedToUserId = userId;
        ticket.AssignedToUserName = userName;
        if (ticket.Status == TicketStatus.New) ticket.Status = TicketStatus.Open;
        ticket.UpdatedAt = DateTime.UtcNow;

        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == ticket.ConversationId, cancellationToken);
        if (conversation != null)
        {
            conversation.AssignedToUserId = userId;
            conversation.AssignedToUserName = userName;
            conversation.AssignedAt = DateTime.UtcNow;
            conversation.Status = MailStatus.Assigned;
        }

        await context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<ServiceTicket?> ResolveAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket == null) return null;

        ticket.Status = TicketStatus.Resolved;
        ticket.ResolvedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;

        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == ticket.ConversationId, cancellationToken);
        if (conversation != null)
        {
            conversation.Status = MailStatus.Resolved;
            conversation.ResolvedAt = DateTime.UtcNow;
        }

        await context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task<ServiceTicket?> CloseAsync(string ticketId, CancellationToken cancellationToken = default)
    {
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket == null) return null;

        ticket.Status = TicketStatus.Closed;
        ticket.ClosedAt = DateTime.UtcNow;
        ticket.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
        return ticket;
    }

    public async Task SyncSlaFromConversationAsync(string conversationId, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId, cancellationToken);
        if (conversation == null) return;

        var ticket = await context.ServiceTickets
            .FirstOrDefaultAsync(t => t.ConversationId == conversationId, cancellationToken);
        if (ticket == null) return;

        ticket.FirstResponseAt = conversation.FirstRespondedAt ?? ticket.FirstResponseAt;
        ticket.IsSlaBreached = conversation.IsSlaBreached;
        if (conversation.ResolvedAt != null)
        {
            ticket.ResolvedAt = conversation.ResolvedAt;
            ticket.Status = TicketStatus.Resolved;
        }
        ticket.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);
    }

    public static bool IsOpen(TicketStatus status) => OpenTicketStatuses.Contains(status);

    private static void SyncFromConversation(ServiceTicket ticket, Conversation conversation)
    {
        ticket.Subject = conversation.Title;
        ticket.Channel = conversation.Channel;
        ticket.ContactId = conversation.ContactId;
        ticket.SharedInboxId = conversation.SharedInboxId;
        ticket.AssignedToUserId = conversation.AssignedToUserId;
        ticket.AssignedToUserName = conversation.AssignedToUserName;
        ticket.FirstResponseAt = conversation.FirstRespondedAt ?? ticket.FirstResponseAt;
        ticket.IsSlaBreached = conversation.IsSlaBreached;
        ticket.Tags = conversation.Labels.ToList();

        if (conversation.Status == MailStatus.Resolved || conversation.Status == MailStatus.Archived)
        {
            ticket.Status = TicketStatus.Resolved;
            ticket.ResolvedAt ??= conversation.ResolvedAt ?? DateTime.UtcNow;
        }
        else if (!string.IsNullOrEmpty(conversation.AssignedToUserId) && ticket.Status == TicketStatus.New)
        {
            ticket.Status = TicketStatus.Open;
        }
    }

    private async Task<int> NextTicketNumberAsync(string tenantId, CancellationToken cancellationToken)
    {
        var max = await context.ServiceTickets
            .IgnoreQueryFilters()
            .Where(t => t.TenantId == tenantId)
            .MaxAsync(t => (int?)t.Number, cancellationToken);
        return (max ?? 0) + 1;
    }
}
