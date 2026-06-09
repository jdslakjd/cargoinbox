using System.Text.RegularExpressions;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

/// <summary>Links legacy Customer records on conversations to unified Contact profiles (erxes customer → contact).</summary>
public class ContactConvergenceService(CargoInboxContext context, ContactCaptureService contactCapture)
{
    public record SyncResult(int ConversationsLinked, int CustomersProcessed, int AlreadyLinked);

    public async Task<SyncResult> SyncCustomerLinksAsync(string tenantId)
    {
        var conversations = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId && c.ContactId == null && c.CustomerId != null)
            .ToListAsync();

        var linked = 0;
        foreach (var conv in conversations)
        {
            var customer = await context.Customers
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == conv.CustomerId);
            if (customer == null || string.IsNullOrWhiteSpace(customer.Email)) continue;

            conv.ContactId = await contactCapture.GetOrCreateContactAsync(customer.Email, tenantId);
            linked++;
        }

        if (linked > 0)
            await context.SaveChangesAsync();

        var withCustomer = await context.Conversations
            .IgnoreQueryFilters()
            .CountAsync(c => c.TenantId == tenantId && c.CustomerId != null);

        return new SyncResult(linked, conversations.Count, withCustomer - conversations.Count);
    }

    public async Task EnsureConversationContactFromEmailAsync(
        Conversation conversation,
        string? fromAddress,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(conversation.ContactId)) return;

        var email = ContactCaptureService.ExtractEmailAddress(fromAddress);
        if (string.IsNullOrEmpty(email)) return;

        conversation.ContactId = await contactCapture.GetOrCreateContactAsync(email, tenantId, cancellationToken);
    }
}
