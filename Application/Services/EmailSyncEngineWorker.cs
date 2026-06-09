using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MimeKit;
using StackExchange.Redis;

namespace CargoInbox.Application.Services;

public class EmailSyncEngineWorker(
    IServiceScopeFactory scopeFactory,
    IConnectionMultiplexer redis,
    IConfiguration configuration,
    ILogger<EmailSyncEngineWorker> logger,
    GmailApiService gmailApi,
    OutlookApiService outlookApi,
    EmailThreadingService emailThreading) : BackgroundService
{
    private readonly IDatabase _redisDb = redis.GetDatabase();
    private static readonly HttpClient _httpClient = new();
    private static readonly SemaphoreSlim _concurrencyGate = new(5);
    private const int MaxConsecutiveFailures = 5;
    private const int SuspensionMinutes = 15;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CargoInbox 增量邮件同步引擎已启动（多租户并发模式）...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                List<UserMailConfig> activeConfigs;
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                    activeConfigs = await db.UserMailConfigs
                        .IgnoreQueryFilters()
                        .Where(c => !c.IsSuspended || (c.SuspendedUntil != null && c.SuspendedUntil < DateTime.UtcNow))
                        .ToListAsync(stoppingToken);
                }

                if (activeConfigs.Count > 0)
                    logger.LogInformation("检测到 {Count} 个活跃邮箱配置待同步", activeConfigs.Count);

                List<SharedInbox> activeInboxes;
                using (var scope = scopeFactory.CreateScope())
                {
                    var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                    activeInboxes = await db.SharedInboxes
                        .Where(x => x.IsActive)
                        .ToListAsync(stoppingToken);
                }

                if (activeInboxes.Count > 0)
                    logger.LogInformation("检测到 {Count} 个公共收件箱渠道待同步", activeInboxes.Count);

                await Parallel.ForEachAsync(activeConfigs, stoppingToken, async (config, ct) =>
                {
                    await _concurrencyGate.WaitAsync(ct);
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                        var rulesProcessor = scope.ServiceProvider.GetRequiredService<RulesEngineProcessor>();
                        await SyncSingleInboxAsync(config, dbContext, rulesProcessor, ct);
                    }
                    finally
                    {
                        _concurrencyGate.Release();
                    }
                });

                foreach (var sharedInbox in activeInboxes)
                {
                    if (stoppingToken.IsCancellationRequested) break;
                    try
                    {
                        using var scope = scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                        var rulesProcessor = scope.ServiceProvider.GetRequiredService<RulesEngineProcessor>();
                        await SyncSharedInboxAsync(sharedInbox, dbContext, rulesProcessor, stoppingToken);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "公共渠道 [{Name}] ({Addr}) 同步异常", sharedInbox.Name, sharedInbox.EmailAddress);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "邮件同步流水线发生未捕获异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }

    private async Task SyncSingleInboxAsync(
        UserMailConfig config, CargoInboxContext dbContext, RulesEngineProcessor rulesProcessor, CancellationToken token)
    {
        if (config.IsSuspended && config.SuspendedUntil > DateTime.UtcNow) return;

        logger.LogInformation("同步邮箱 {Email} (类型: {ProviderType})", config.EmailAddress, config.ProviderType);

        if (config.ProviderType == MailProviderType.Gmail_OAuth2)
        {
            await SyncGmailInboxAsync(config, dbContext, rulesProcessor, token);
            return;
        }

        if (config.ProviderType == MailProviderType.Outlook_Office365)
        {
            await SyncOutlookInboxAsync(config, dbContext, rulesProcessor, token);
            return;
        }

        var redisKey = $"cargoinbox:sync:last_uid:{config.Id}";
        var lastUidStr = await _redisDb.StringGetAsync(redisKey);
        uint lastUid = lastUidStr.IsNullOrEmpty ? config.LastSyncUid : uint.Parse(lastUidStr.ToString());

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(config.ImapHost, config.ImapPort, true, token);
            await client.AuthenticateAsync(config.EmailAddress, config.EncryptedAppPassword, token);

            lastUid = await ProcessImapFolderAsync(
                client.Inbox, lastUid, redisKey, config, null, dbContext, rulesProcessor,
                "IMAP 同步", token);

            if (client.IsConnected) await client.DisconnectAsync(true);
            config.ConsecutiveFailureCount = 0;
            config.IsSuspended = false;
            config.SuspendedUntil = null;
            await dbContext.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            await HandleSyncFailureAsync(config, dbContext, ex);
        }
    }

    private async Task SyncOutlookInboxAsync(
        UserMailConfig config, CargoInboxContext dbContext, RulesEngineProcessor rulesProcessor, CancellationToken token)
    {
        var redisKey = $"cargoinbox:sync:last_uid:{config.Id}";
        var lastUidStr = await _redisDb.StringGetAsync(redisKey);
        uint lastUid = lastUidStr.IsNullOrEmpty ? config.LastSyncUid : uint.Parse(lastUidStr.ToString());

        try
        {
            var session = await outlookApi.GetSessionAsync(config.UserId, config.EmailAddress);
            if (session == null)
            {
                logger.LogWarning("Outlook 邮箱 {Email} 无可用 OAuth Token", config.EmailAddress);
                return;
            }

            using var client = new ImapClient();
            await client.ConnectAsync(config.ImapHost, config.ImapPort, MailKit.Security.SecureSocketOptions.SslOnConnect, token);
            var oauth2 = new SaslMechanismOAuth2(config.EmailAddress, session.AccessToken);
            await client.AuthenticateAsync(oauth2, token);

            lastUid = await ProcessImapFolderAsync(
                client.Inbox, lastUid, redisKey, config, null, dbContext, rulesProcessor,
                "Outlook IMAP 同步", token);

            if (client.IsConnected) await client.DisconnectAsync(true);
            config.ConsecutiveFailureCount = 0;
            config.IsSuspended = false;
            config.SuspendedUntil = null;
            await dbContext.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            await HandleSyncFailureAsync(config, dbContext, ex);
        }
    }

    private async Task SyncSharedInboxAsync(
        SharedInbox inbox, CargoInboxContext dbContext, RulesEngineProcessor rulesProcessor, CancellationToken token)
    {
        var redisKey = $"cargoinbox:sync:shared_uid:{inbox.Id}";
        var lastUidStr = await _redisDb.StringGetAsync(redisKey);
        uint lastUid = lastUidStr.IsNullOrEmpty ? inbox.LastSyncUid : uint.Parse(lastUidStr.ToString());

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(inbox.ImapHost, inbox.ImapPort, true, token);
            await client.AuthenticateAsync(inbox.EmailAddress, inbox.EncryptedPassword, token);

            lastUid = await ProcessImapFolderAsync(
                client.Inbox, lastUid, redisKey, null, inbox, dbContext, rulesProcessor,
                "公共渠道同步", token);

            if (client.IsConnected) await client.DisconnectAsync(true);
        }
        catch (Exception ex) { logger.LogError(ex, "公共渠道 [{Name}] 同步异常", inbox.Name); }
    }

    private async Task<uint> ProcessImapFolderAsync(
        IMailFolder? folder,
        uint lastUid,
        string redisKey,
        UserMailConfig? config,
        SharedInbox? sharedInbox,
        CargoInboxContext dbContext,
        RulesEngineProcessor rulesProcessor,
        string activityDetailPrefix,
        CancellationToken token)
    {
        if (folder == null) return lastUid;
        await folder.OpenAsync(FolderAccess.ReadOnly, token);

        IList<IMessageSummary> fetched;
        if (lastUid > 0)
        {
            var range = new UniqueIdRange(new UniqueId(lastUid + 1), UniqueId.MaxValue);
            fetched = await folder.FetchAsync(range, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
        }
        else
        {
            int startIdx = Math.Max(0, folder.Count - 20);
            fetched = await folder.FetchAsync(startIdx, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
        }

        foreach (var summary in fetched)
        {
            if (token.IsCancellationRequested) break;

            var uidStr = summary.UniqueId.ToString();
            var exists = await dbContext.ConversationMessages
                .IgnoreQueryFilters()
                .AnyAsync(m => m.Id == uidStr, token);
            if (exists) continue;

            var message = await folder.GetMessageAsync(summary.UniqueId, token);
            var ingested = await IngestEmailMessageAsync(
                message, uidStr, config, sharedInbox, null, dbContext, rulesProcessor,
                activityDetailPrefix, token);
            if (!ingested) continue;

            lastUid = summary.UniqueId.Id;
            await _redisDb.StringSetAsync(redisKey, lastUid.ToString());

            if (config != null)
                config.LastSyncUid = lastUid;
            else if (sharedInbox != null)
                sharedInbox.LastSyncUid = lastUid;

            await dbContext.SaveChangesAsync(token);
        }

        return lastUid;
    }

    private async Task SyncGmailInboxAsync(
        UserMailConfig config, CargoInboxContext dbContext, RulesEngineProcessor rulesProcessor, CancellationToken token)
    {
        var redisKey = $"cargoinbox:sync:gmail_history_id:{config.Id}";
        try
        {
            var session = await gmailApi.GetSessionAsync(config.UserId, config.EmailAddress);
            if (session == null) { logger.LogWarning("Gmail 邮箱 {Email} 无可用 OAuth Token", config.EmailAddress); return; }

            var lastHistoryIdStr = await _redisDb.StringGetAsync(redisKey);
            ulong lastHistoryId = lastHistoryIdStr.IsNullOrEmpty ? 0 : ulong.Parse(lastHistoryIdStr.ToString());
            string? query = lastHistoryId > 0 ? $"after:{lastHistoryId}" : null;

            var messages = await gmailApi.ListMessagesAsync(session.AccessToken, query ?? "");
            if (messages.Count == 0) { logger.LogInformation("Gmail 邮箱 {Email} 无新邮件", config.EmailAddress); return; }

            foreach (var msgSummary in messages)
            {
                if (token.IsCancellationRequested) break;
                var exists = await dbContext.ConversationMessages.IgnoreQueryFilters().AnyAsync(m => m.Id == msgSummary.Id, token);
                if (exists) continue;

                var rawRfc2822 = await gmailApi.GetMessageRawAsync(session.AccessToken, msgSummary.Id);
                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(rawRfc2822));
                var message = await MimeMessage.LoadAsync(stream, token);

                await IngestEmailMessageAsync(
                    message, msgSummary.Id, config, null, msgSummary.ThreadId,
                    dbContext, rulesProcessor, "Gmail API 同步", token);

                var profile = await gmailApi.GetProfileHistoryIdAsync(session.AccessToken);
                await _redisDb.StringSetAsync(redisKey, profile.ToString());
                config.LastSyncUid = (uint)(profile & 0xFFFFFFFF);
            }

            config.ConsecutiveFailureCount = 0;
            config.IsSuspended = false;
            config.SuspendedUntil = null;
            await dbContext.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            await HandleSyncFailureAsync(config, dbContext, ex);
        }
    }

    private async Task<bool> IngestEmailMessageAsync(
        MimeMessage message,
        string localMessageId,
        UserMailConfig? config,
        SharedInbox? sharedInbox,
        string? gmailThreadId,
        CargoInboxContext dbContext,
        RulesEngineProcessor rulesProcessor,
        string activityDetailPrefix,
        CancellationToken token)
    {
        var tenantId = config?.TenantId ?? sharedInbox!.TenantId;
        var userId = config?.UserId ?? "shared";
        var sharedInboxId = sharedInbox?.Id;
        var internetMessageId = EmailThreadingService.NormalizeMessageId(message.MessageId);

        if (await emailThreading.IsDuplicateInternetMessageAsync(dbContext, tenantId, internetMessageId, token))
            return false;

        var textContent = message.TextBody ?? string.Empty;
        var subject = message.Subject ?? "(无主题)";

        if (string.IsNullOrWhiteSpace(textContent) && !string.IsNullOrWhiteSpace(message.HtmlBody))
            textContent = GetEmbeddingText(null, message.HtmlBody);

        foreach (var attachment in message.Attachments)
        {
            if (attachment is MimeKit.MimePart mimePart && mimePart.Content != null)
            {
                using var memoryStream = new MemoryStream();
                await mimePart.Content.DecodeToAsync(memoryStream, token);
                memoryStream.Position = 0;
                var fileName = mimePart.FileName ?? $"attachment_{Guid.NewGuid():N}";
                using var attachmentScope = scopeFactory.CreateScope();
                var storageService = attachmentScope.ServiceProvider.GetRequiredService<AttachmentStorageService>();
                var mimeType = mimePart.ContentType?.MimeType ?? "application/octet-stream";
                await storageService.StoreAsync(memoryStream, fileName, mimeType, localMessageId);
            }
        }

        var conversation = await emailThreading.FindThreadAsync(
            dbContext, message, tenantId, userId, sharedInboxId, gmailThreadId, token);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                UserId = userId,
                TenantId = tenantId,
                SharedInboxId = sharedInboxId,
                Title = subject,
                Channel = MessageChannel.Email,
                Status = MailStatus.Open,
                LastMessageAt = message.Date.UtcDateTime
            };
            if (!string.IsNullOrEmpty(gmailThreadId))
                EmailThreadingService.EnsureGmailThreadLabel(conversation, gmailThreadId);
            dbContext.Conversations.Add(conversation);
            await dbContext.SaveChangesAsync(token);
        }
        else if (!string.IsNullOrEmpty(gmailThreadId))
        {
            EmailThreadingService.EnsureGmailThreadLabel(conversation, gmailThreadId);
        }

        var convMsg = new ConversationMessage
        {
            Id = localMessageId,
            TenantId = tenantId,
            ConversationId = conversation.Id,
            FromAddress = message.From.ToString(),
            ToAddress = message.To.ToString(),
            Subject = subject,
            TextBody = textContent,
            HtmlBody = message.HtmlBody ?? textContent,
            DateTime = message.Date.UtcDateTime,
            InternetMessageId = internetMessageId
        };

        if (!string.IsNullOrEmpty(convMsg.HtmlBody))
            convMsg.HtmlBody = await ReplaceCidImagesAsync(message, convMsg.HtmlBody, localMessageId, token);

        try
        {
            var embedText = GetEmbeddingText(textContent, message.HtmlBody);
            if (!string.IsNullOrEmpty(embedText))
            {
                var apiKey = configuration["AISettings:ApiKey"];
                var endpoint = configuration["AISettings:Endpoint"];
                var modelId = configuration["AISettings:EmbeddingModelId"];
                var requestBody = new { model = modelId, input = new[] { embedText } };
                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var response = await _httpClient.PostAsJsonAsync(endpoint + "/embeddings", requestBody, token);
                if (response.IsSuccessStatusCode)
                {
                    var jsonResult = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: token);
                    var embeddingArray = jsonResult.GetProperty("data")[0].GetProperty("embedding").EnumerateArray().Select(x => x.GetSingle()).ToArray();
                    convMsg.Embedding = new Pgvector.Vector(embeddingArray);
                }
            }
        }
        catch (Exception aiEx) { logger.LogWarning(aiEx, "AI 向量化失败，跳过"); }

        dbContext.ConversationMessages.Add(convMsg);
        conversation.LastMessageAt = convMsg.DateTime;
        await dbContext.SaveChangesAsync(token);

        if (config != null && config.ProviderType == MailProviderType.Gmail_OAuth2)
        {
            var mail = new Mail
            {
                TenantId = config.TenantId,
                UserId = config.UserId,
                MailConfigId = config.Id,
                ProviderMessageId = localMessageId,
                FromAddress = convMsg.FromAddress,
                ToAddress = convMsg.ToAddress,
                Subject = convMsg.Subject,
                TextBody = convMsg.TextBody,
                HtmlBody = convMsg.HtmlBody,
                DateTime = convMsg.DateTime,
                IsRead = false,
                Labels = ["INBOX"],
                Embedding = convMsg.Embedding,
                Status = MailStatus.Open
            };
            dbContext.Mails.Add(mail);
            await dbContext.SaveChangesAsync(token);
        }

        await rulesProcessor.ProcessConversationAsync(conversation, convMsg);

        dbContext.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conversation.Id,
            UserId = userId,
            UserName = config?.EmailAddress ?? sharedInbox?.Name ?? userId,
            Action = sharedInbox != null ? "SharedInboxSynced" : "EmailSynced",
            Detail = $"{activityDetailPrefix}: {subject}"
        });
        await dbContext.SaveChangesAsync(token);
        return true;
    }

    private async Task HandleSyncFailureAsync(UserMailConfig config, CargoInboxContext dbContext, Exception ex)
    {
        config.ConsecutiveFailureCount++;
        if (config.ConsecutiveFailureCount >= MaxConsecutiveFailures)
        {
            config.IsSuspended = true;
            config.SuspendedUntil = DateTime.UtcNow.AddMinutes(SuspensionMinutes);
            logger.LogError(ex, "邮箱 {Email} 连续失败 {Count} 次，已熔断至 {Until}", config.EmailAddress, MaxConsecutiveFailures, config.SuspendedUntil);
        }
        else
        {
            logger.LogError(ex, "邮箱 {Email} 同步失败 ({FailureCount}/{Max})", config.EmailAddress, config.ConsecutiveFailureCount, MaxConsecutiveFailures);
        }
        await dbContext.SaveChangesAsync();
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

    private async Task<string> ReplaceCidImagesAsync(MimeKit.MimeMessage message, string htmlBody, string messageId, CancellationToken token)
    {
        if (!htmlBody.Contains("cid:", StringComparison.OrdinalIgnoreCase)) return htmlBody;
        foreach (var bodyPart in message.BodyParts)
        {
            if (bodyPart is not MimeKit.MimePart mimePart || string.IsNullOrEmpty(mimePart.ContentId) || mimePart.Content == null) continue;
            var cid = mimePart.ContentId.Trim('<', '>');
            if (!htmlBody.Contains(cid, StringComparison.OrdinalIgnoreCase)) continue;
            try
            {
                using var memoryStream = new MemoryStream();
                await mimePart.Content.DecodeToAsync(memoryStream, token);
                memoryStream.Position = 0;
                var fileName = mimePart.FileName ?? $"inline_{Guid.NewGuid():N}.{mimePart.ContentType?.MediaSubtype ?? "png"}";
                using var scope = scopeFactory.CreateScope();
                var storageService = scope.ServiceProvider.GetRequiredService<AttachmentStorageService>();
                var attachment = await storageService.StoreAsync(memoryStream, fileName, mimePart.ContentType?.MimeType ?? "image/png", messageId);
                htmlBody = htmlBody.Replace($"cid:{cid}", attachment.FileUrl);
            }
            catch { /* keep original */ }
        }
        return htmlBody;
    }
}
