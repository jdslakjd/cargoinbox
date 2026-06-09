using CargoInbox.Core.Entities;
using CargoInbox.Core.Interfaces;
using CargoInbox.Infrastructure.Data;
using MailKit;
using MailKit.Net.Imap;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace CargoInbox.Application.Services;

public class MailSyncService(
    IServiceScopeFactory scopeFactory,
    IEmbeddingService embeddingService) : IMailSyncService
{
    public async Task SyncUserMailsAsync(string configId)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var config = await context.UserMailConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.Id == configId);

        if (config == null) return;

        using var client = new ImapClient();
        try
        {
            await client.ConnectAsync(config.ImapHost, config.ImapPort, true);
            await client.AuthenticateAsync(config.EmailAddress, config.EncryptedAppPassword);

            var inbox = client.Inbox;
            if (inbox == null) return;
            await inbox.OpenAsync(FolderAccess.ReadOnly);

            uint lastUidValue = 0;
            var latestLocalMail = await context.Mails
                .IgnoreQueryFilters()
                .Where(m => m.MailConfigId == configId && m.ProviderMessageId != null)
                .OrderByDescending(m => m.DateTime)
                .FirstOrDefaultAsync();

            if (latestLocalMail?.ProviderMessageId != null && uint.TryParse(latestLocalMail.ProviderMessageId, out var parsedUid))
            {
                lastUidValue = parsedUid;
            }

            IList<IMessageSummary> fetched;
            if (lastUidValue > 0)
            {
                var range = new UniqueIdRange(new UniqueId(lastUidValue + 1), UniqueId.MaxValue);
                fetched = await inbox.FetchAsync(range, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope);
            }
            else
            {
                int startIdx = Math.Max(0, inbox.Count - 20);
                fetched = await inbox.FetchAsync(startIdx, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope);
            }

            foreach (var summary in fetched)
            {
                var uidStr = summary.UniqueId.ToString();

                var exists = await context.Mails.IgnoreQueryFilters()
                    .AnyAsync(m => m.MailConfigId == configId && m.ProviderMessageId == uidStr);
                if (exists) continue;

                var message = await inbox.GetMessageAsync(summary.UniqueId);
                var textContent = message.TextBody ?? string.Empty;

                // Extract text from HTML when TextBody is empty
                if (string.IsNullOrWhiteSpace(textContent) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    textContent = GetEmbeddingText(null, message.HtmlBody);
                }

                float[] embeddingVector = Array.Empty<float>();
                var embedText = GetEmbeddingText(textContent, message.HtmlBody);
                if (!string.IsNullOrWhiteSpace(embedText))
                {
                    try
                    {
                        embeddingVector = await embeddingService.GetEmbeddingAsync(embedText);
                    }
                    catch
                    {
                        embeddingVector = Array.Empty<float>();
                    }
                }

                var newMail = new Mail
                {
                    UserId = config.UserId,
                    MailConfigId = config.Id,
                    ProviderMessageId = uidStr,
                    FromAddress = message.From.ToString(),
                    ToAddress = message.To.ToString(),
                    Subject = message.Subject ?? "（无主题）",
                    TextBody = textContent,
                    HtmlBody = message.HtmlBody ?? textContent,
                    DateTime = message.Date.UtcDateTime,
                    IsRead = false,
                    Labels = ["INBOX"],
                    Embedding = embeddingVector.Length > 0 ? new Pgvector.Vector(embeddingVector) : null
                };

                context.Mails.Add(newMail);
            }

            await context.SaveChangesAsync();
        }
        finally
        {
            if (client.IsConnected)
            {
                await client.DisconnectAsync(true);
            }
        }
    }

    private static string GetEmbeddingText(string? textBody, string? htmlBody)
    {
        var text = !string.IsNullOrWhiteSpace(textBody) ? textBody : htmlBody;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
        text = System.Net.WebUtility.HtmlDecode(text);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
        return text.Length > 1000 ? text[..1000] : text;
    }
}
