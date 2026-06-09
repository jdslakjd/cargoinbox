using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using MailKit;
using MailKit.Net.Imap;
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
    GmailApiService gmailApi) : BackgroundService
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

        var redisKey = $"cargoinbox:sync:last_uid:{config.Id}";
        var lastUidStr = await _redisDb.StringGetAsync(redisKey);
        uint lastUid = lastUidStr.IsNullOrEmpty ? config.LastSyncUid : uint.Parse(lastUidStr.ToString());

        try
        {
            using var client = new ImapClient();
            await client.ConnectAsync(config.ImapHost, config.ImapPort, true, token);
            await client.AuthenticateAsync(config.EmailAddress, config.EncryptedAppPassword, token);

            var inbox = client.Inbox;
            if (inbox == null) return;
            await inbox.OpenAsync(FolderAccess.ReadOnly, token);

            IList<IMessageSummary> fetched;
            if (lastUid > 0)
            {
                var range = new UniqueIdRange(new UniqueId(lastUid + 1), UniqueId.MaxValue);
                fetched = await inbox.FetchAsync(range, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
            }
            else
            {
                int startIdx = Math.Max(0, inbox.Count - 20);
                fetched = await inbox.FetchAsync(startIdx, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
            }

            foreach (var summary in fetched)
            {
                if (token.IsCancellationRequested) break;

                var uidStr = summary.UniqueId.ToString();
                var exists = await dbContext.ConversationMessages
                    .IgnoreQueryFilters()
                    .AnyAsync(m => m.Id == uidStr, token);
                if (exists) continue;

                var message = await inbox.GetMessageAsync(summary.UniqueId, token);
                var textContent = message.TextBody ?? string.Empty;
                var subject = message.Subject ?? "(无主题)";

                // Extract text from HTML when TextBody is empty
                if (string.IsNullOrWhiteSpace(textContent) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    textContent = GetEmbeddingText(null, message.HtmlBody);
                }

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
                        await storageService.StoreAsync(memoryStream, fileName, mimeType, uidStr);
                    }
                }

                var subjectPrefix = subject.Length > 15 ? subject[..15] : subject;
                var conversation = await dbContext.Conversations
                    .IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.UserId == config.UserId
                        && c.Title != null && c.Title.Contains(subjectPrefix), token);

                if (conversation == null)
                {
                    conversation = new Conversation
                    {
                        UserId = config.UserId,
                        TenantId = config.TenantId,
                        Title = subject,
                        Channel = MessageChannel.Email,
                        Status = MailStatus.Open,
                        LastMessageAt = message.Date.UtcDateTime
                    };
                    dbContext.Conversations.Add(conversation);
                    await dbContext.SaveChangesAsync(token);
                }

                var convMsg = new ConversationMessage
                {
                    Id = uidStr,
                    TenantId = config.TenantId,
                    ConversationId = conversation.Id,
                    FromAddress = message.From.ToString(),
                    ToAddress = message.To.ToString(),
                    Subject = subject,
                    TextBody = textContent,
                    HtmlBody = message.HtmlBody ?? textContent,
                    DateTime = message.Date.UtcDateTime
                };

                if (!string.IsNullOrEmpty(convMsg.HtmlBody))
                    convMsg.HtmlBody = await ReplaceCidImagesAsync(message, convMsg.HtmlBody, uidStr, token);

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

                await rulesProcessor.ProcessConversationAsync(conversation, convMsg);

                lastUid = summary.UniqueId.Id;
                await _redisDb.StringSetAsync(redisKey, lastUid.ToString());
                config.LastSyncUid = lastUid;

                dbContext.ActivityLogs.Add(new ActivityLog
                {
                    ConversationId = conversation.Id,
                    UserId = config.UserId,
                    UserName = config.EmailAddress,
                    Action = "EmailSynced",
                    Detail = $"IMAP 同步: {subject}"
                });
                await dbContext.SaveChangesAsync(token);
            }

            if (client.IsConnected) await client.DisconnectAsync(true);
            config.ConsecutiveFailureCount = 0;
            config.IsSuspended = false;
            config.SuspendedUntil = null;
            await dbContext.SaveChangesAsync(token);
        }
        catch (Exception ex)
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
            await dbContext.SaveChangesAsync(token);
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

            var imapInbox = client.Inbox;
            if (imapInbox == null) return;
            await imapInbox.OpenAsync(FolderAccess.ReadOnly, token);

            IList<IMessageSummary> fetched;
            if (lastUid > 0)
            {
                var range = new UniqueIdRange(new UniqueId(lastUid + 1), UniqueId.MaxValue);
                fetched = await imapInbox.FetchAsync(range, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
            }
            else
            {
                int startIdx = Math.Max(0, imapInbox.Count - 20);
                fetched = await imapInbox.FetchAsync(startIdx, -1, MessageSummaryItems.UniqueId | MessageSummaryItems.Envelope, token);
            }

            foreach (var summary in fetched)
            {
                if (token.IsCancellationRequested) break;
                var uidStr = summary.UniqueId.ToString();
                var exists = await dbContext.ConversationMessages.IgnoreQueryFilters().AnyAsync(m => m.Id == uidStr, token);
                if (exists) continue;

                var message = await imapInbox.GetMessageAsync(summary.UniqueId, token);
                var subject = message.Subject ?? "(无主题)";

                var conversation = new Conversation
                {
                    TenantId = inbox.TenantId,
                    UserId = "shared",
                    SharedInboxId = inbox.Id,
                    Title = subject,
                    Channel = MessageChannel.Email,
                    Status = MailStatus.Open,
                    LastMessageAt = message.Date.UtcDateTime
                };
                dbContext.Conversations.Add(conversation);
                await dbContext.SaveChangesAsync(token);

                var convMsg = new ConversationMessage
                {
                    Id = uidStr,
                    TenantId = inbox.TenantId,
                    ConversationId = conversation.Id,
                    FromAddress = message.From.ToString(),
                    ToAddress = message.To.ToString(),
                    Subject = subject,
                    TextBody = message.TextBody ?? string.Empty,
                    HtmlBody = message.HtmlBody ?? message.TextBody ?? string.Empty,
                    DateTime = message.Date.UtcDateTime
                };

                dbContext.ConversationMessages.Add(convMsg);
                conversation.LastMessageAt = convMsg.DateTime;
                await dbContext.SaveChangesAsync(token);
                await rulesProcessor.ProcessConversationAsync(conversation, convMsg);

                lastUid = summary.UniqueId.Id;
                await _redisDb.StringSetAsync(redisKey, lastUid.ToString());
                inbox.LastSyncUid = lastUid;

                dbContext.ActivityLogs.Add(new ActivityLog { ConversationId = conversation.Id, UserId = "shared", UserName = inbox.Name, Action = "SharedInboxSynced", Detail = $"公共渠道同步: {subject}" });
                await dbContext.SaveChangesAsync(token);
            }
            if (client.IsConnected) await client.DisconnectAsync(true);
        }
        catch (Exception ex) { logger.LogError(ex, "公共渠道 [{Name}] 同步异常", inbox.Name); }
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
                var textContent = message.TextBody ?? string.Empty;
                var subject = message.Subject ?? "(无主题)";

                // Extract text from HTML when TextBody is empty
                if (string.IsNullOrWhiteSpace(textContent) && !string.IsNullOrWhiteSpace(message.HtmlBody))
                {
                    textContent = GetEmbeddingText(null, message.HtmlBody);
                }

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
                        await storageService.StoreAsync(memoryStream, fileName, mimeType, msgSummary.Id);
                    }
                }

                var subjectPrefix = subject.Length > 15 ? subject[..15] : subject;
                var conversation = await dbContext.Conversations.IgnoreQueryFilters()
                    .FirstOrDefaultAsync(c => c.UserId == config.UserId && c.Title != null && c.Title.Contains(subjectPrefix), token);

                if (conversation == null)
                {
                    conversation = new Conversation { UserId = config.UserId, TenantId = config.TenantId, Title = subject, Channel = MessageChannel.Email, Status = MailStatus.Open, LastMessageAt = message.Date.UtcDateTime };
                    dbContext.Conversations.Add(conversation);
                    await dbContext.SaveChangesAsync(token);
                }

                var convMsg = new ConversationMessage
                {
                    Id = msgSummary.Id, TenantId = config.TenantId, ConversationId = conversation.Id,
                    FromAddress = message.From.ToString(), ToAddress = message.To.ToString(),
                    Subject = subject, TextBody = textContent, HtmlBody = message.HtmlBody ?? textContent, DateTime = message.Date.UtcDateTime
                };

                if (!string.IsNullOrEmpty(convMsg.HtmlBody))
                    convMsg.HtmlBody = await ReplaceCidImagesAsync(message, convMsg.HtmlBody, msgSummary.Id, token);

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

                var mail = new Mail
                {
                    TenantId = config.TenantId, UserId = config.UserId, MailConfigId = config.Id, ProviderMessageId = msgSummary.Id,
                    FromAddress = convMsg.FromAddress, ToAddress = convMsg.ToAddress, Subject = convMsg.Subject,
                    TextBody = convMsg.TextBody, HtmlBody = convMsg.HtmlBody, DateTime = convMsg.DateTime,
                    IsRead = false, Labels = ["INBOX"], Embedding = convMsg.Embedding, Status = MailStatus.Open
                };
                dbContext.Mails.Add(mail);
                await dbContext.SaveChangesAsync(token);

                await rulesProcessor.ProcessConversationAsync(conversation, convMsg);

                var profile = await gmailApi.GetProfileHistoryIdAsync(session.AccessToken);
                await _redisDb.StringSetAsync(redisKey, profile.ToString());
                config.LastSyncUid = (uint)(profile & 0xFFFFFFFF);

                dbContext.ActivityLogs.Add(new ActivityLog { ConversationId = conversation.Id, UserId = config.UserId, UserName = config.EmailAddress, Action = "EmailSynced", Detail = $"Gmail API 同步: {subject}" });
                await dbContext.SaveChangesAsync(token);
            }

            config.ConsecutiveFailureCount = 0;
            config.IsSuspended = false;
            config.SuspendedUntil = null;
            await dbContext.SaveChangesAsync(token);
        }
        catch (Exception ex)
        {
            config.ConsecutiveFailureCount++;
            if (config.ConsecutiveFailureCount >= MaxConsecutiveFailures) { config.IsSuspended = true; config.SuspendedUntil = DateTime.UtcNow.AddMinutes(SuspensionMinutes); }
            else logger.LogError(ex, "Gmail 邮箱 {Email} 同步失败 ({FailureCount}/{Max})", config.EmailAddress, config.ConsecutiveFailureCount, MaxConsecutiveFailures);
            await dbContext.SaveChangesAsync(token);
        }
    }

    private static string GetEmbeddingText(string? textBody, string? htmlBody)
    {
        var text = !string.IsNullOrWhiteSpace(textBody) ? textBody : htmlBody;
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        // Remove style and script blocks
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<style[^>]*>[\s\S]*?</style>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        text = System.Text.RegularExpressions.Regex.Replace(text, @"<script[^>]*>[\s\S]*?</script>", "", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        // Strip HTML tags
        text = System.Text.RegularExpressions.Regex.Replace(text, "<[^>]+>", " ");
        // Decode HTML entities (&amp; &lt; &#39; etc.)
        text = System.Net.WebUtility.HtmlDecode(text);
        // Normalize whitespace
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