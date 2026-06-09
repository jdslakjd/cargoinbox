using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MimeKit;

namespace CargoInbox.Application.Services;

public sealed class MailSendOptions
{
    public string? ToAddress { get; set; }
    public string? CcAddress { get; set; }
    public string? BccAddress { get; set; }
    public List<string>? AttachmentIds { get; set; }
    public bool BypassApproval { get; set; }
    public bool PersistMessage { get; set; } = true;
}

public class MailSendService(IServiceScopeFactory scopeFactory)
{
    public async Task<ConversationMessage?> SendFromConversationAsync(
        string conversationId,
        string userId,
        string subject,
        string htmlBody,
        string? textBody,
        string? ccAddress,
        MailSendOptions? options = null)
    {
        options ??= new MailSendOptions();
        if (!string.IsNullOrWhiteSpace(ccAddress) && string.IsNullOrWhiteSpace(options.CcAddress))
            options.CcAddress = ccAddress;

        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        if (!options.BypassApproval)
        {
            var approvalService = scope.ServiceProvider.GetRequiredService<ApprovalWorkflowService>();
            if (await approvalService.RequiresApprovalAsync(userId))
            {
                await approvalService.SubmitForApprovalAsync(
                    conversationId, userId, subject, htmlBody, textBody, options.CcAddress);
                context.ActivityLogs.Add(new ActivityLog
                {
                    ConversationId = conversationId,
                    UserId = userId,
                    UserName = "系统",
                    Action = "ApprovalSubmitted",
                    Detail = $"待审批: {subject}"
                });
                await context.SaveChangesAsync();
                return null;
            }
        }

        var storageService = scope.ServiceProvider.GetRequiredService<AttachmentStorageService>();
        var gmailApi = scope.ServiceProvider.GetRequiredService<GmailApiService>();
        var outlookApi = scope.ServiceProvider.GetRequiredService<OutlookApiService>();
        return await SendDirectAsync(context, storageService, gmailApi, outlookApi, conversationId, userId, subject, htmlBody, textBody, options);
    }

    private static string ExtractEmail(string address)
    {
        if (string.IsNullOrWhiteSpace(address)) return string.Empty;
        var trimmed = address.Trim();
        var lt = trimmed.IndexOf('<');
        var gt = trimmed.IndexOf('>');
        if (lt >= 0 && gt > lt)
            return trimmed[(lt + 1)..gt].Trim();
        return trimmed;
    }

    private static void AddAddresses(InternetAddressList list, string? addresses)
    {
        if (string.IsNullOrWhiteSpace(addresses)) return;
        foreach (var part in addresses.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var email = ExtractEmail(part);
            if (!string.IsNullOrEmpty(email) && email.Contains('@'))
                list.Add(new MailboxAddress(email, email));
        }
    }

    private static string ResolveRecipient(Conversation conversation, UserMailConfig config, MailSendOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.ToAddress))
            return ExtractEmail(options.ToAddress);

        if (!string.IsNullOrWhiteSpace(conversation.Contact?.Email))
            return conversation.Contact.Email.Trim();

        var outboundFrom = config.EmailAddress;
        var inbound = conversation.Messages
            .OrderByDescending(m => m.DateTime)
            .FirstOrDefault(m =>
                !string.IsNullOrWhiteSpace(m.FromAddress)
                && !m.FromAddress.Equals(outboundFrom, StringComparison.OrdinalIgnoreCase)
                && !m.FromAddress.StartsWith("outbound@", StringComparison.OrdinalIgnoreCase));

        if (inbound != null)
            return ExtractEmail(inbound.FromAddress);

        return ExtractEmail(conversation.Messages.FirstOrDefault()?.FromAddress ?? "");
    }

    private async Task AuthenticateSmtpAsync(
        SmtpClient smtp,
        UserMailConfig config,
        string userId,
        GmailApiService gmailApi,
        OutlookApiService outlookApi)
    {
        if (config.ProviderType == MailProviderType.Gmail_OAuth2)
        {
            var session = await gmailApi.GetSessionAsync(userId, config.EmailAddress);
            if (session == null)
                throw new InvalidOperationException("Gmail OAuth token not found. Reconnect your Google account in Settings → Mailboxes.");

            var oauth2 = new SaslMechanismOAuth2(config.EmailAddress, session.AccessToken);
            await smtp.AuthenticateAsync(oauth2);
            return;
        }

        if (config.ProviderType == MailProviderType.Outlook_Office365)
        {
            var session = await outlookApi.GetSessionAsync(userId, config.EmailAddress);
            if (session == null)
                throw new InvalidOperationException("Outlook OAuth token not found. Reconnect your Microsoft account in Settings → Mailboxes.");

            var oauth2 = new SaslMechanismOAuth2(config.EmailAddress, session.AccessToken);
            await smtp.AuthenticateAsync(oauth2);
            return;
        }

        if (string.IsNullOrWhiteSpace(config.EncryptedAppPassword))
            throw new InvalidOperationException("Mailbox password is missing. Update credentials in Settings → Mailboxes.");

        await smtp.AuthenticateAsync(config.EmailAddress, config.EncryptedAppPassword);
    }

    private async Task AttachFilesAsync(
        BodyBuilder bodyBuilder,
        CargoInboxContext context,
        AttachmentStorageService storageService,
        List<string> attachmentIds)
    {
        var attachments = await context.Attachments
            .Where(a => attachmentIds.Contains(a.Id))
            .ToListAsync();

        foreach (var att in attachments)
        {
            var stream = await storageService.RetrieveAsync(att.Id);
            if (stream == null) continue;

            var contentType = string.IsNullOrWhiteSpace(att.ContentType)
                ? ContentType.Parse("application/octet-stream")
                : ContentType.Parse(att.ContentType);
            await bodyBuilder.Attachments.AddAsync(att.FileName, stream, contentType);
        }
    }

    private async Task<ConversationMessage?> SendDirectAsync(
        CargoInboxContext context,
        AttachmentStorageService storageService,
        GmailApiService gmailApi,
        OutlookApiService outlookApi,
        string conversationId,
        string userId,
        string subject,
        string htmlBody,
        string? textBody,
        MailSendOptions options)
    {
        var conversation = await context.Conversations
            .IgnoreQueryFilters()
            .Include(c => c.Messages)
            .Include(c => c.Contact)
            .FirstOrDefaultAsync(c => c.Id == conversationId);

        if (conversation == null) throw new InvalidOperationException("会话不存在");

        var config = await context.UserMailConfigs
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.UserId == userId);

        if (config == null) throw new InvalidOperationException("未找到该用户的邮件发送配置");

        var toAddress = ResolveRecipient(conversation, config, options);
        if (string.IsNullOrWhiteSpace(toAddress) || !toAddress.Contains('@'))
            throw new InvalidOperationException("无法确定收件人地址，请检查联系人邮箱或会话消息");

        var signature = await context.UserSignatures
            .FirstOrDefaultAsync(s => s.UserId == userId && s.IsDefault);

        var signedBody = htmlBody + (signature?.HtmlContent ?? "");

        using var smtp = new SmtpClient();
        await smtp.ConnectAsync(config.SmtpHost, config.SmtpPort, SecureSocketOptions.Auto);
        await AuthenticateSmtpAsync(smtp, config, userId, gmailApi, outlookApi);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(config.EmailAddress, config.EmailAddress));
        AddAddresses(message.To, toAddress);
        AddAddresses(message.Cc, options.CcAddress);
        AddAddresses(message.Bcc, options.BccAddress);
        message.Subject = subject;

        var bodyBuilder = new BodyBuilder { HtmlBody = signedBody, TextBody = textBody ?? string.Empty };
        if (options.AttachmentIds?.Count > 0)
            await AttachFilesAsync(bodyBuilder, context, storageService, options.AttachmentIds);

        message.Body = bodyBuilder.ToMessageBody();

        await smtp.SendAsync(message);
        await smtp.DisconnectAsync(true);

        conversation.LastMessageAt = DateTime.UtcNow;
        if (conversation.Status != MailStatus.Snoozed && conversation.Status != MailStatus.Resolved)
            conversation.Status = MailStatus.Open;

        ConversationMessage? savedMessage = null;
        if (options.PersistMessage)
        {
            savedMessage = new ConversationMessage
            {
                ConversationId = conversationId,
                TenantId = conversation.TenantId,
                FromAddress = config.EmailAddress,
                ToAddress = toAddress,
                Subject = subject,
                TextBody = textBody ?? string.Empty,
                HtmlBody = signedBody,
                DateTime = DateTime.UtcNow,
                Type = MessageType.Email
            };
            context.ConversationMessages.Add(savedMessage);
        }

        if (options.AttachmentIds?.Count > 0)
        {
            var linked = await context.Attachments
                .Where(a => options.AttachmentIds.Contains(a.Id))
                .ToListAsync();
            foreach (var att in linked)
            {
                if (savedMessage != null)
                    att.MessageId = savedMessage.Id;
                att.TenantId = conversation.TenantId;
            }
        }

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conversationId,
            UserId = userId,
            UserName = config.EmailAddress,
            Action = "EmailSent",
            Detail = $"已发送至 {toAddress}: {subject}"
        });

        await context.SaveChangesAsync();
        return savedMessage;
    }
}
