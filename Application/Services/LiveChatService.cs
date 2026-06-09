using CargoInbox.Api.Hubs;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class LiveChatService(
    CargoInboxContext context,
    ContactCaptureService contactCapture,
    InboundConversationService inboundConversation,
    TicketService ticketService,
    IHubContext<CollaborationHub> hubContext)
{
    public record WidgetPublicConfig(
        string Name,
        string WelcomeMessage,
        string OfflineMessage,
        string PrimaryColor,
        string Position,
        bool IsEnabled);

    public record SessionResult(
        string SessionToken,
        string ConversationId,
        string ContactId,
        WidgetPublicConfig Widget,
        IReadOnlyList<ChatMessageDto> Messages);

    public record ChatMessageDto(
        string Id,
        string Text,
        bool IsAgent,
        string? AgentName,
        DateTime SentAt);

    public async Task<LiveChatWidget?> GetWidgetByPublicKeyAsync(string publicKey, CancellationToken cancellationToken = default)
    {
        return await context.LiveChatWidgets
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(w => w.PublicKey == publicKey, cancellationToken);
    }

    public async Task<LiveChatWidget> EnsureDefaultWidgetAsync(string tenantId, CancellationToken cancellationToken = default)
    {
        var widget = await context.LiveChatWidgets
            .FirstOrDefaultAsync(w => w.TenantId == tenantId, cancellationToken);

        if (widget != null) return widget;

        widget = new LiveChatWidget
        {
            TenantId = tenantId,
            PublicKey = $"ci_{Guid.NewGuid():N}"
        };
        context.LiveChatWidgets.Add(widget);
        await context.SaveChangesAsync(cancellationToken);
        return widget;
    }

    public async Task<SessionResult?> StartOrResumeSessionAsync(
        string publicKey,
        string visitorId,
        string? visitorName,
        string? visitorEmail,
        CancellationToken cancellationToken = default)
    {
        var widget = await GetWidgetByPublicKeyAsync(publicKey, cancellationToken);
        if (widget == null || !widget.IsEnabled) return null;

        var contactId = await contactCapture.GetOrCreateLiveChatVisitorAsync(
            visitorId, visitorName, visitorEmail, widget.TenantId);

        var session = await context.LiveChatSessions
            .IgnoreQueryFilters()
            .Where(s => s.WidgetId == widget.Id && s.VisitorId == visitorId)
            .OrderByDescending(s => s.LastActiveAt)
            .FirstOrDefaultAsync(cancellationToken);

        Conversation conversation;
        var isNewConversation = false;

        if (session != null)
        {
            conversation = await context.Conversations
                .IgnoreQueryFilters()
                .FirstAsync(c => c.Id == session.ConversationId, cancellationToken);

            if (conversation.Status is MailStatus.Resolved or MailStatus.Archived or MailStatus.Trash)
            {
                conversation = await CreateLiveChatConversationAsync(
                    widget, contactId, visitorId, visitorName, cancellationToken);
                session.ConversationId = conversation.Id;
                isNewConversation = true;
            }

            session.LastActiveAt = DateTime.UtcNow;
            if (!string.IsNullOrWhiteSpace(visitorName)) session.VisitorName = visitorName.Trim();
            if (!string.IsNullOrWhiteSpace(visitorEmail)) session.VisitorEmail = visitorEmail.Trim();
        }
        else
        {
            conversation = await CreateLiveChatConversationAsync(
                widget, contactId, visitorId, visitorName, cancellationToken);
            isNewConversation = true;

            session = new LiveChatSession
            {
                TenantId = widget.TenantId,
                WidgetId = widget.Id,
                VisitorId = visitorId,
                ContactId = contactId,
                ConversationId = conversation.Id,
                VisitorName = visitorName,
                VisitorEmail = visitorEmail
            };
            context.LiveChatSessions.Add(session);

            context.ActivityLogs.Add(new ActivityLog
            {
                TenantId = widget.TenantId,
                ConversationId = conversation.Id,
                UserId = "livechat-widget",
                UserName = "Live Chat",
                Action = "LiveChatSessionStarted",
                Detail = $"Visitor {visitorId}"
            });
        }

        await context.SaveChangesAsync(cancellationToken);

        if (isNewConversation)
        {
            await hubContext.Clients.Group(widget.TenantId).SendAsync(
                "OnGlobalNewConversationReceived",
                new
                {
                    conversationId = conversation.Id,
                    channel = "LiveChat",
                    snippet = "New live chat session"
                },
                cancellationToken);
        }

        var messages = await LoadMessagesAsync(conversation.Id, null, cancellationToken);
        return new SessionResult(
            session.Id,
            conversation.Id,
            contactId,
            ToPublicConfig(widget),
            messages);
    }

    private async Task<Conversation> CreateLiveChatConversationAsync(
        LiveChatWidget widget,
        string contactId,
        string visitorId,
        string? visitorName,
        CancellationToken cancellationToken)
    {
        var title = !string.IsNullOrWhiteSpace(visitorName)
            ? $"Live chat: {visitorName.Trim()}"
            : $"Live chat: {visitorId[..Math.Min(8, visitorId.Length)]}";

        var conversation = new Conversation
        {
            TenantId = widget.TenantId,
            UserId = "livechat-widget",
            Title = title,
            Channel = MessageChannel.LiveChat,
            Status = MailStatus.Open,
            ContactId = contactId,
            LastMessageAt = DateTime.UtcNow
        };
        context.Conversations.Add(conversation);
        await context.SaveChangesAsync(cancellationToken);
        await ticketService.EnsureForConversationAsync(conversation, tryAutoAssign: true, cancellationToken: cancellationToken);
        return conversation;
    }

    public async Task<ChatMessageDto?> SendVisitorMessageAsync(
        string sessionToken,
        string messageText,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(messageText)) return null;

        var session = await context.LiveChatSessions
            .IgnoreQueryFilters()
            .Include(s => s.Widget)
            .FirstOrDefaultAsync(s => s.Id == sessionToken, cancellationToken);

        if (session?.Widget == null || !session.Widget.IsEnabled) return null;

        session.LastActiveAt = DateTime.UtcNow;

        var (_, message, isNew) = await inboundConversation.AppendOrCreateAsync(
            new InboundConversationService.InboundMessageRequest(
                session.TenantId,
                session.ContactId,
                MessageChannel.LiveChat,
                LiveChatVisitorAddress(session.VisitorId),
                "livechat@cargoinbox.cn",
                "Live chat message",
                messageText.Trim(),
                $"<p>{messageText.Trim()}</p>",
                "livechat-widget",
                session.VisitorName != null
                    ? $"Live chat: {session.VisitorName}"
                    : $"Live chat: {session.VisitorId[..Math.Min(8, session.VisitorId.Length)]}"),
            cancellationToken);

        if (session.ConversationId != message.ConversationId)
            session.ConversationId = message.ConversationId;

        context.ActivityLogs.Add(new ActivityLog
        {
            TenantId = session.TenantId,
            ConversationId = message.ConversationId,
            UserId = "livechat-widget",
            UserName = session.VisitorName ?? "Visitor",
            Action = "LiveChatMessage",
            Detail = messageText.Length > 200 ? messageText[..200] : messageText
        });
        await context.SaveChangesAsync(cancellationToken);

        if (isNew)
        {
            await hubContext.Clients.Group(session.TenantId).SendAsync(
                "OnGlobalNewConversationReceived",
                new
                {
                    conversationId = message.ConversationId,
                    channel = "LiveChat",
                    snippet = messageText.Length > 120 ? messageText[..120] : messageText
                },
                cancellationToken);
        }
        else
        {
            await hubContext.Clients.Group($"conversation_{message.ConversationId}").SendAsync(
                "OnNewMessageReceived",
                new
                {
                    conversationId = message.ConversationId,
                    messageSnippet = messageText.Length > 120 ? messageText[..120] : messageText,
                    senderName = session.VisitorName ?? "Visitor"
                },
                cancellationToken);
        }

        return MapMessage(message);
    }

    public async Task<IReadOnlyList<ChatMessageDto>> GetMessagesAsync(
        string sessionToken,
        DateTime? since,
        CancellationToken cancellationToken = default)
    {
        var session = await context.LiveChatSessions
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.Id == sessionToken, cancellationToken);

        if (session == null) return [];

        session.LastActiveAt = DateTime.UtcNow;
        await context.SaveChangesAsync(cancellationToken);

        return await LoadMessagesAsync(session.ConversationId, since, cancellationToken);
    }

    public static WidgetPublicConfig ToPublicConfig(LiveChatWidget widget) =>
        new(widget.Name, widget.WelcomeMessage, widget.OfflineMessage, widget.PrimaryColor, widget.Position, widget.IsEnabled);

    public static string LiveChatVisitorAddress(string visitorId) => $"visitor:{visitorId}@livechat.cargoinbox";

    private async Task<IReadOnlyList<ChatMessageDto>> LoadMessagesAsync(
        string conversationId,
        DateTime? since,
        CancellationToken cancellationToken)
    {
        var query = context.ConversationMessages
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(m => m.ConversationId == conversationId);

        if (since.HasValue)
            query = query.Where(m => m.DateTime > since.Value);

        var rows = await query
            .OrderBy(m => m.DateTime)
            .Take(200)
            .ToListAsync(cancellationToken);

        return rows.Select(MapMessage).ToList();
    }

    private static ChatMessageDto MapMessage(ConversationMessage message)
    {
        var isAgent = IsAgentMessage(message.FromAddress);
        return new ChatMessageDto(
            message.Id,
            message.TextBody,
            isAgent,
            isAgent ? ExtractAgentName(message) : null,
            message.DateTime);
    }

    public static bool IsAgentMessage(string fromAddress) =>
        fromAddress.StartsWith("outbound@", StringComparison.OrdinalIgnoreCase)
        || fromAddress.StartsWith("agent:", StringComparison.OrdinalIgnoreCase);

    private static string? ExtractAgentName(ConversationMessage message)
    {
        if (message.FromAddress.StartsWith("agent:", StringComparison.OrdinalIgnoreCase))
        {
            var part = message.FromAddress.Split('@')[0];
            return part.Length > 6 ? "Support" : "Agent";
        }
        if (message.Subject.StartsWith("Reply from ", StringComparison.OrdinalIgnoreCase))
            return message.Subject["Reply from ".Length..];
        return "Support";
    }
}
