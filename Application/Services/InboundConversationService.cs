using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class InboundConversationService(
    CargoInboxContext context,
    RulesEngineProcessor rulesEngine,
    IEmbeddingService embeddingService)
{
    public record InboundMessageRequest(
        string TenantId,
        string ContactId,
        MessageChannel Channel,
        string FromAddress,
        string ToAddress,
        string Subject,
        string TextBody,
        string HtmlBody,
        string UserId,
        string Title);

    private static readonly MailStatus[] ActiveThreadStatuses =
        [MailStatus.Open, MailStatus.Assigned, MailStatus.Snoozed];

    public async Task<(Conversation Conversation, ConversationMessage Message, bool IsNewConversation)>
        AppendOrCreateAsync(InboundMessageRequest request, CancellationToken cancellationToken = default)
    {
        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == request.TenantId
                && c.ContactId == request.ContactId
                && c.Channel == request.Channel
                && ActiveThreadStatuses.Contains(c.Status))
            .OrderByDescending(c => c.LastMessageAt)
            .FirstOrDefaultAsync(cancellationToken);

        var isNew = conversation == null;
        if (isNew)
        {
            conversation = new Conversation
            {
                TenantId = request.TenantId,
                UserId = request.UserId,
                Title = request.Title,
                Channel = request.Channel,
                Status = MailStatus.Open,
                ContactId = request.ContactId,
                LastMessageAt = DateTime.UtcNow
            };
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync(cancellationToken);
        }
        else
        {
            if (conversation!.Status == MailStatus.Snoozed)
            {
                conversation.Status = MailStatus.Open;
                conversation.SnoozedUntil = null;
            }
        }

        var message = new ConversationMessage
        {
            TenantId = request.TenantId,
            ConversationId = conversation.Id,
            FromAddress = request.FromAddress,
            ToAddress = request.ToAddress,
            Subject = request.Subject,
            TextBody = request.TextBody,
            HtmlBody = request.HtmlBody,
            DateTime = DateTime.UtcNow,
            Type = MessageType.InstantMessage
        };
        context.ConversationMessages.Add(message);
        conversation.LastMessageAt = message.DateTime;
        await context.SaveChangesAsync(cancellationToken);

        if (!string.IsNullOrWhiteSpace(request.TextBody))
            await embeddingService.VectorizeAndSaveAsync(message.Id, request.TextBody);

        await rulesEngine.ProcessConversationAsync(conversation, message);

        return (conversation, message, isNew);
    }
}
