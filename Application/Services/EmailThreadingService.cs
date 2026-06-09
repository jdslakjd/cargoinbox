using System.Text.RegularExpressions;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using MimeKit;

namespace CargoInbox.Application.Services;

public class EmailThreadingService
{
    private static readonly Regex SubjectPrefixRegex = new(
        @"^\s*(?:(?:re|fwd|fw|回复|转发)\s*[\[\]:]?\s*)+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? NormalizeMessageId(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        raw = raw.Trim();
        if (raw.StartsWith('<') && raw.EndsWith('>'))
            return raw;
        return $"<{raw.Trim('<', '>')}>";
    }

    public static string NormalizeSubject(string? subject)
    {
        if (string.IsNullOrWhiteSpace(subject)) return string.Empty;
        var s = subject.Trim();
        while (true)
        {
            var next = SubjectPrefixRegex.Replace(s, "");
            if (next == s) break;
            s = next.Trim();
        }
        return s;
    }

    public static string GmailThreadLabel(string threadId) => $"gmail-thread:{threadId}";

    public static void EnsureGmailThreadLabel(Conversation conversation, string threadId)
    {
        var label = GmailThreadLabel(threadId);
        if (!conversation.Labels.Contains(label))
            conversation.Labels.Add(label);
    }

    public async Task<bool> IsDuplicateInternetMessageAsync(
        CargoInboxContext db, string tenantId, string? internetMessageId, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(internetMessageId)) return false;
        return await db.ConversationMessages
            .IgnoreQueryFilters()
            .AnyAsync(m => m.TenantId == tenantId && m.InternetMessageId == internetMessageId, ct);
    }

    public async Task<Conversation?> FindThreadAsync(
        CargoInboxContext db,
        MimeMessage message,
        string tenantId,
        string? userId,
        string? sharedInboxId,
        string? gmailThreadId,
        CancellationToken ct)
    {
        if (!string.IsNullOrEmpty(gmailThreadId) && !string.IsNullOrEmpty(userId))
        {
            var label = GmailThreadLabel(gmailThreadId);
            var gmailConv = await db.Conversations
                .IgnoreQueryFilters()
                .Where(c => c.TenantId == tenantId
                    && c.UserId == userId
                    && c.SharedInboxId == null
                    && c.Labels.Contains(label))
                .OrderByDescending(c => c.LastMessageAt)
                .FirstOrDefaultAsync(ct);
            if (gmailConv != null) return gmailConv;
        }

        var inReplyTo = NormalizeMessageId(message.InReplyTo);
        if (inReplyTo != null)
        {
            var conv = await FindConversationByInternetMessageIdAsync(
                db, inReplyTo, tenantId, userId, sharedInboxId, ct);
            if (conv != null) return conv;
        }

        if (message.References is { Count: > 0 })
        {
            for (var i = message.References.Count - 1; i >= 0; i--)
            {
                var normalized = NormalizeMessageId(message.References[i]);
                if (normalized == null) continue;
                var conv = await FindConversationByInternetMessageIdAsync(
                    db, normalized, tenantId, userId, sharedInboxId, ct);
                if (conv != null) return conv;
            }
        }

        var normalizedSubject = NormalizeSubject(message.Subject);
        if (string.IsNullOrEmpty(normalizedSubject)) return null;

        var query = db.Conversations
            .IgnoreQueryFilters()
            .Where(c => c.TenantId == tenantId
                && c.Channel == MessageChannel.Email
                && c.Status != MailStatus.Trash
                && c.Status != MailStatus.Spam
                && c.Title != null);

        if (!string.IsNullOrEmpty(sharedInboxId))
            query = query.Where(c => c.SharedInboxId == sharedInboxId);
        else if (!string.IsNullOrEmpty(userId))
            query = query.Where(c => c.UserId == userId && c.SharedInboxId == null);

        var candidates = await query
            .OrderByDescending(c => c.LastMessageAt)
            .Take(50)
            .ToListAsync(ct);

        return candidates.FirstOrDefault(c => NormalizeSubject(c.Title) == normalizedSubject);
    }

    private static async Task<Conversation?> FindConversationByInternetMessageIdAsync(
        CargoInboxContext db,
        string messageId,
        string tenantId,
        string? userId,
        string? sharedInboxId,
        CancellationToken ct)
    {
        var query = from m in db.ConversationMessages.IgnoreQueryFilters()
                    join c in db.Conversations.IgnoreQueryFilters() on m.ConversationId equals c.Id
                    where m.TenantId == tenantId && m.InternetMessageId == messageId
                    select c;

        if (!string.IsNullOrEmpty(sharedInboxId))
            query = query.Where(c => c.SharedInboxId == sharedInboxId);
        else if (!string.IsNullOrEmpty(userId))
            query = query.Where(c => c.UserId == userId && c.SharedInboxId == null);

        return await query.OrderByDescending(c => c.LastMessageAt).FirstOrDefaultAsync(ct);
    }
}
