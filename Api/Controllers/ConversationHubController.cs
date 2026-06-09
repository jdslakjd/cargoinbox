using System.Security.Claims;
using CargoInbox.Api.Hubs;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/conversations")]
public class ConversationHubController(
    CargoInboxContext context,
    RedisCollaborationService redisService,
    ContactCaptureService contactService,
    IHubContext<CollaborationHub> hubContext,
    MailSendService mailSendService,
    ITenantProvider tenantProvider,
    AiTranslationService translationService,
    SlaTrackerService slaTrackerService,
    ChannelOutboundService channelOutboundService,
    InboxPermissionService inboxPermissionService,
    TicketService ticketService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";
    private bool IsAdmin => InboxPermissionService.IsAdmin(User);

    private async Task<IActionResult?> DenyUnlessCanAccessAsync(string conversationId)
    {
        if (!await inboxPermissionService.CanAccessConversationAsync(
                CurrentUserId, tenantProvider.TenantId, IsAdmin, conversationId))
            return Forbid();
        return null;
    }

    private async Task<IActionResult?> DenyUnlessAdminAsync()
    {
        if (!IsAdmin) return Forbid();
        return null;
    }

    [HttpGet]
    public async Task<IActionResult> GetConversations(
        [FromQuery] MailStatus? status,
        [FromQuery] int? channel,
        [FromQuery] string? assignedToUserId,
        [FromQuery] string? subscribedByUserId,
        [FromQuery] DateTime? before,
        [FromQuery] string? tags,
        [FromQuery] string? folder,
        [FromQuery] string? sharedInboxId,
        [FromQuery] int limit = 30)
    {
        limit = Math.Min(limit, 50);
        var cursor = before ?? DateTime.MaxValue;

        if (!string.IsNullOrEmpty(sharedInboxId)
            && !await inboxPermissionService.CanAccessSharedInboxAsync(
                CurrentUserId, tenantProvider.TenantId, IsAdmin, sharedInboxId))
        {
            return Forbid();
        }

        var allowedInboxIds = await inboxPermissionService.GetAllowedSharedInboxIdsAsync(
            CurrentUserId, tenantProvider.TenantId, IsAdmin);

        var query = context.Conversations
            .AsNoTracking()
            .Include(c => c.Messages.OrderByDescending(m => m.DateTime).Take(1))
            .Include(c => c.Contact)
            .Include(c => c.Comments.OrderByDescending(m => m.CreatedAt).Take(1))
            .Where(c => c.LastMessageAt < cursor)
            .AsQueryable();

        query = inboxPermissionService.ApplyConversationAccessFilter(
            query, CurrentUserId, IsAdmin, allowedInboxIds);

        if (status.HasValue) query = query.Where(c => c.Status == status.Value);
        if (channel.HasValue) query = query.Where(c => c.Channel == (MessageChannel)channel.Value);
        if (!string.IsNullOrEmpty(assignedToUserId)) query = query.Where(c => c.AssignedToUserId == assignedToUserId);
        if (!string.IsNullOrEmpty(subscribedByUserId)) query = query.Where(c => c.SubscriberIds.Contains(subscribedByUserId));
        if (!string.IsNullOrEmpty(sharedInboxId)) query = query.Where(c => c.SharedInboxId == sharedInboxId);
        if (!string.IsNullOrEmpty(tags))
        {
            var tagList = tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var tag in tagList)
                query = query.Where(c => c.Labels.Contains(tag.Trim()));
        }

        if (folder == "drafts")
        {
            var draftConvIds = await context.ConversationDrafts
                .Where(d => d.CreatedByUserId == CurrentUserId)
                .Select(d => d.ConversationId)
                .Distinct()
                .ToListAsync();
            query = query.Where(c => draftConvIds.Contains(c.Id));
        }
        else if (folder == "sent")
        {
            var userEmails = await context.UserMailConfigs
                .Where(c => c.UserId == CurrentUserId)
                .Select(c => c.EmailAddress)
                .ToListAsync();
            var sentConvIds = await context.ConversationMessages
                .Where(m => userEmails.Contains(m.FromAddress) || m.FromAddress.Contains("outbound@cargoinbox"))
                .Select(m => m.ConversationId)
                .Distinct()
                .ToListAsync();
            query = query.Where(c => sentConvIds.Contains(c.Id));
        }

        var results = await query
            .OrderByDescending(c => c.LastMessageAt)
            .Take(limit)
            .ToListAsync();

        var nextCursor = results.Count == limit ? results.Last().LastMessageAt : (DateTime?)null;

        return Ok(new
        {
            data = results.Select(c => new
            {
                c.Id, c.Title, c.Channel, c.Status, c.AssignedToUserId, c.LastMessageAt, c.Labels, c.ContactId, c.CustomerId, c.SharedInboxId,
                c.AssignedToUserName, c.AssignedAt, c.SubscriberIds, c.IsSlaBreached,
                SenderName = c.Contact?.Name ?? MailAddressParser.ExtractDisplayName(c.Messages.FirstOrDefault()?.FromAddress),
                SenderEmail = MailAddressParser.ExtractEmail(c.Messages.FirstOrDefault()?.FromAddress),
                Subject = c.Title,
                Snippet = SnippetHelper.GetSnippet(c.Messages.FirstOrDefault()),
                LatestComment = c.Comments.FirstOrDefault() != null ? new {
                    c.Comments.FirstOrDefault()!.Content,
                    c.Comments.FirstOrDefault()!.UserName,
                    AttachmentCount = context.Attachments.Count(a => a.CommentId == c.Comments.FirstOrDefault()!.Id)
                } : null
            }),
            nextCursor
        });
    }

    [HttpPost("rules")]
    public async Task<IActionResult> CreateRule([FromBody] AutomationRule rule)
    {
        var denied = await DenyUnlessAdminAsync();
        if (denied != null) return denied;

        if (string.IsNullOrWhiteSpace(rule.Name) || string.IsNullOrWhiteSpace(rule.ConditionKeyword))
            return BadRequest(new { message = "规则名称与关键词不能为空" });

        rule.TenantId = tenantProvider.TenantId;
        context.AutomationRules.Add(rule);
        await context.SaveChangesAsync();
        return Ok(rule);
    }

    [HttpGet("rules")]
    public async Task<IActionResult> GetRules()
    {
        var rules = await context.AutomationRules
            .AsNoTracking()
            .Where(r => r.TenantId == tenantProvider.TenantId)
            .OrderByDescending(r => r.IsActive)
            .ThenBy(r => r.Name)
            .ToListAsync();
        return Ok(rules);
    }

    [HttpDelete("rules/{id}")]
    public async Task<IActionResult> DeleteRule(string id)
    {
        var denied = await DenyUnlessAdminAsync();
        if (denied != null) return denied;

        var rule = await context.AutomationRules.FirstOrDefaultAsync(r => r.Id == id);
        if (rule == null) return NotFound(new { message = "规则不存在" });
        context.AutomationRules.Remove(rule);
        await context.SaveChangesAsync();
        return Ok(new { message = "规则已删除" });
    }

    [HttpPost("{id}/reply")]
    public async Task<IActionResult> Reply(string id, [FromBody] ReplyRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.Include(c => c.Contact).FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        // Auto-capture contact if not linked yet
        if (string.IsNullOrEmpty(conv.ContactId) && !string.IsNullOrEmpty(request.ToAddress))
        {
            conv.ContactId = await contactService.GetOrCreateContactAsync(request.ToAddress);
        }

        await mailSendService.SendFromConversationAsync(
            id, CurrentUserId, $"Re: {conv.Title}", $"<p>{request.TextBody}</p>", request.TextBody, request.Cc,
            new MailSendOptions { ToAddress = conv.Contact?.Email ?? request.ToAddress });
        await slaTrackerService.MarkFirstResponseAsync(id);

        context.MailComments.Add(new MailComment
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Content = $"{CurrentUserName} 发送了回复邮件。"
        });
        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "ReplySent",
            Detail = $"回复: {(request.TextBody.Length > 80 ? request.TextBody[..80] + "..." : request.TextBody)}"
        });
        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"conversation_{id}").SendAsync("OnNewMessageReceived", new
        {
            conversationId = id,
            messageSnippet = request.TextBody.Length > 120 ? request.TextBody[..120] + "..." : request.TextBody,
            senderName = CurrentUserName
        });

        return Ok(new { message = "回复成功" });
    }

    [HttpPost("{id}/snooze")]
    public async Task<IActionResult> SnoozeConversation(string id, [FromBody] SnoozeRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Status = MailStatus.Snoozed;
        conv.SnoozedUntil = request.Until.ToUniversalTime();

        context.MailComments.Add(new MailComment
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Content = $"会话已由 {CurrentUserName} 设置为稍后提醒，将于 {(conv.SnoozedUntil.HasValue ? conv.SnoozedUntil.Value.ToString("yyyy-MM-dd HH:mm") : "未知")} 恢复。"
        });
        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Snoozed",
            Detail = $"稍后提醒至 {(conv.SnoozedUntil.HasValue ? conv.SnoozedUntil.Value.ToString("yyyy-MM-dd HH:mm") : "未知")}"
        });
        await context.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Snoozed",
            actorName = CurrentUserName
        });

        return Ok(new { message = "已进入稍后提醒队列", snoozedUntil = conv.SnoozedUntil });
    }

    [HttpPost("{id}/unsnooze")]
    public async Task<IActionResult> UnsnoozeConversation(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Status = MailStatus.Open;
        conv.SnoozedUntil = null;

        context.MailComments.Add(new MailComment
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Content = $"{CurrentUserName} 恢复了该会话。"
        });
        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Unsnoozed",
            Detail = "会话已恢复"
        });
        await context.SaveChangesAsync();

        await hubContext.Clients.All.SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Open",
            actorName = CurrentUserName
        });

        return Ok(new { message = "会话已恢复" });
    }

    [HttpPost("{id}/heartbeat-lock")]
    public async Task<IActionResult> AcquireHeartbeatLock(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var success = await redisService.TryAcquireCollisionLockAsync(id, CurrentUserId, CurrentUserName);
        if (success)
        {
            await hubContext.Clients.All.SendAsync("OnConversationLocked", new
            {
                conversationId = id,
                lockedByUserId = CurrentUserId,
                lockedByUserName = CurrentUserName
            });
            return Ok(new { message = "锁定成功" });
        }
        else
        {
            var occupant = await redisService.GetCollisionStatusAsync(id);
            return StatusCode(409, new { message = "另一名成员正在处理此会话", details = occupant });
        }
    }

    [HttpPost("{id}/unlock")]
    public async Task<IActionResult> ReleaseLock(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        await redisService.ReleaseCollisionLockAsync(id, CurrentUserId);
        await hubContext.Clients.All.SendAsync("OnConversationUnlocked", new { conversationId = id });

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Unlocked",
            Detail = "释放协同锁"
        });
        await context.SaveChangesAsync();
        return Ok(new { message = "已释放锁" });
    }

    [HttpGet("{id}/contact-profile")]
    public async Task<IActionResult> GetContactProfile(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.AsNoTracking()
            .Include(c => c.Contact!)
                .ThenInclude(ct => ct.LinkedCompany)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });
        if (conv.Contact == null) return Ok(null);

        var c = conv.Contact;
        return Ok(new
        {
            c.Id,
            c.TenantId,
            c.Name,
            c.Email,
            c.Phone,
            c.Company,
            c.CompanyId,
            CompanyName = c.LinkedCompany?.Name ?? c.Company,
            c.OwnerUserId,
            c.OwnerUserName,
            c.Tags,
            c.Notes,
            c.LeadSource,
            c.LifecycleStatus,
            c.CreatedAt,
            c.UpdatedAt
        });
    }

    [HttpPut("contacts/{contactId}")]
    public async Task<IActionResult> UpdateContact(string contactId, [FromBody] Contact updatedData)
    {
        var contact = await context.Contacts.FirstOrDefaultAsync(c => c.Id == contactId);
        if (contact == null) return NotFound(new { message = "通讯录中不存在该联系人" });

        contact.Name = updatedData.Name;
        contact.Phone = updatedData.Phone;
        contact.Company = updatedData.Company;
        contact.Notes = updatedData.Notes;

        await context.SaveChangesAsync();
        return Ok(contact);
    }

    [HttpPost("{id}/unified-reply")]
    public async Task<IActionResult> UnifiedReply(string id, [FromBody] UnifiedReplyRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        if (string.IsNullOrWhiteSpace(request.TextContent))
            return BadRequest(new { error = "回复内容不能为空" });

        var conversation = await context.Conversations.Include(c => c.Contact).FirstOrDefaultAsync(c => c.Id == id);
        if (conversation == null) return NotFound(new { error = "会话不存在" });

        var contact = conversation.Contact;
        if (contact == null) return BadRequest(new { error = "关联的联系人档案丢失" });

        var htmlContent = request.HtmlContent ?? $"<p>{request.TextContent}</p>";
        ConversationMessage? outboundMessage = null;

        try
        {
            switch (conversation.Channel)
            {
                case MessageChannel.Email:
                    if (string.IsNullOrEmpty(contact.Email)) return BadRequest(new { error = "客户邮箱地址不存在" });
                    outboundMessage = await mailSendService.SendFromConversationAsync(
                        conversation.Id, CurrentUserId, $"Re: {conversation.Title}",
                        htmlContent, request.TextContent, request.Cc,
                        new MailSendOptions
                        {
                            ToAddress = contact.Email,
                            BccAddress = request.Bcc,
                            AttachmentIds = request.AttachmentIds
                        });
                    break;
                case MessageChannel.WhatsApp:
                    if (string.IsNullOrEmpty(contact.Phone)) return BadRequest(new { error = "客户手机号不存在" });
                    var waSent = await channelOutboundService.SendWhatsAppAsync(
                        tenantProvider.TenantId, contact.Phone, request.TextContent);
                    if (!waSent) return StatusCode(502, new { error = "WhatsApp 发送失败，请检查 WhatsApp 配置" });
                    outboundMessage = new ConversationMessage
                    {
                        ConversationId = conversation.Id,
                        FromAddress = "outbound@cargoinbox.cn",
                        ToAddress = contact.Phone,
                        Subject = "Reply to: " + conversation.Title,
                        TextBody = request.TextContent,
                        HtmlBody = htmlContent,
                        DateTime = DateTime.UtcNow,
                        Type = MessageType.InstantMessage,
                        TenantId = tenantProvider.TenantId
                    };
                    context.ConversationMessages.Add(outboundMessage);
                    break;
                case MessageChannel.Facebook:
                    if (string.IsNullOrEmpty(contact.FacebookPsid)) return BadRequest(new { error = "Facebook PSID 不存在" });
                    var fbSent = await channelOutboundService.SendFacebookAsync(
                        tenantProvider.TenantId, contact.FacebookPsid, request.TextContent);
                    if (!fbSent) return StatusCode(502, new { error = "Facebook 发送失败，请检查 Facebook 配置" });
                    outboundMessage = new ConversationMessage
                    {
                        ConversationId = conversation.Id,
                        FromAddress = "outbound@cargoinbox.cn",
                        ToAddress = contact.FacebookPsid,
                        Subject = "Reply to: " + conversation.Title,
                        TextBody = request.TextContent,
                        HtmlBody = htmlContent,
                        DateTime = DateTime.UtcNow,
                        Type = MessageType.InstantMessage,
                        TenantId = tenantProvider.TenantId
                    };
                    context.ConversationMessages.Add(outboundMessage);
                    break;
                default:
                    outboundMessage = new ConversationMessage
                    {
                        ConversationId = conversation.Id,
                        FromAddress = "outbound@cargoinbox.cn",
                        ToAddress = contact.Email ?? contact.Phone ?? "Unknown",
                        Subject = "Reply to: " + conversation.Title,
                        TextBody = request.TextContent,
                        HtmlBody = htmlContent,
                        DateTime = DateTime.UtcNow,
                        Type = MessageType.InstantMessage,
                        TenantId = tenantProvider.TenantId
                    };
                    context.ConversationMessages.Add(outboundMessage);
                    break;
            }

            await slaTrackerService.MarkFirstResponseAsync(conversation.Id);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = $"渠道外发失败: {ex.Message}" });
        }

        conversation.LastMessageAt = DateTime.UtcNow;
        conversation.Status = MailStatus.Assigned;
        conversation.AssignedToUserId = CurrentUserId;
        conversation.AssignedToUserName = CurrentUserName;

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conversation.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "UnifiedReply",
            Detail = $"全渠道统一回复: {conversation.Channel}",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();

        await hubContext.Clients.Group(tenantProvider.TenantId).SendAsync("OnConversationReplySent", new
        {
            conversationId = conversation.Id,
            messageId = outboundMessage?.Id,
            senderName = CurrentUserName,
            snippet = request.TextContent.Length > 120 ? request.TextContent[..120] : request.TextContent
        });

        return Ok(new { success = true, messageId = outboundMessage?.Id });
    }

    [HttpGet("{id}/messages")]
    public async Task<IActionResult> GetConversationMessages(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var messages = await context.ConversationMessages
            .AsNoTracking()
            .Where(m => m.ConversationId == id)
            .OrderBy(m => m.DateTime)
            .ToListAsync();

        return Ok(messages);
    }

    [HttpGet("teammates")]
    public async Task<IActionResult> GetTeammates()
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantProvider.TenantId && u.IsActive)
            .Select(u => new { u.Id, u.DisplayName, u.Username, u.Email })
            .ToListAsync();

        return Ok(users);
    }

    [HttpGet("counts")]
    public async Task<IActionResult> GetCounts([FromQuery] string? subscribedByUserId)
    {
        var allowedInboxIds = await inboxPermissionService.GetAllowedSharedInboxIdsAsync(
            CurrentUserId, tenantProvider.TenantId, IsAdmin);

        var query = inboxPermissionService.ApplyConversationAccessFilter(
            context.Conversations.AsNoTracking(),
            CurrentUserId,
            IsAdmin,
            allowedInboxIds);

        var counts = await query.GroupBy(_ => 1).Select(g => new
        {
            total = g.Count(),
            open = g.Count(c => c.Status == MailStatus.Open),
            assigned = g.Count(c => c.Status == MailStatus.Assigned),
            snoozed = g.Count(c => c.Status == MailStatus.Snoozed),
            resolved = g.Count(c => c.Status == MailStatus.Resolved || c.Status == MailStatus.Archived),
            trash = g.Count(c => c.Status == MailStatus.Trash || c.Status == MailStatus.Spam),
            subscribed = !string.IsNullOrEmpty(subscribedByUserId) ? g.Count(c => c.SubscriberIds.Contains(subscribedByUserId)) : 0
        }).FirstOrDefaultAsync();

        var draftCount = await context.ConversationDrafts
            .Where(d => d.CreatedByUserId == CurrentUserId)
            .Select(d => d.ConversationId)
            .Distinct()
            .CountAsync();

        var userEmails = await context.UserMailConfigs
            .Where(c => c.UserId == CurrentUserId)
            .Select(c => c.EmailAddress)
            .ToListAsync();
        var sentCount = await context.ConversationMessages
            .Where(m => userEmails.Contains(m.FromAddress) || m.FromAddress.Contains("outbound@cargoinbox"))
            .Select(m => m.ConversationId)
            .Distinct()
            .CountAsync();

        return Ok(new
        {
            counts?.total,
            counts?.open,
            counts?.assigned,
            counts?.snoozed,
            counts?.resolved,
            counts?.trash,
            counts?.subscribed,
            drafts = draftCount,
            sent = sentCount
        });
    }
    [HttpGet("tags")]
    public async Task<IActionResult> GetTags()
    {
        var allowedInboxIds = await inboxPermissionService.GetAllowedSharedInboxIdsAsync(
            CurrentUserId, tenantProvider.TenantId, IsAdmin);

        var conversations = await inboxPermissionService.ApplyConversationAccessFilter(
                context.Conversations.AsNoTracking(),
                CurrentUserId,
                IsAdmin,
                allowedInboxIds)
            .Select(c => c.Labels)
            .ToListAsync();

        var allTags = conversations
            .SelectMany(l => l)
            .Distinct()
            .OrderBy(t => t)
            .ToList();

        return Ok(allTags);
    }

    [HttpPost("{id}/tags")]
    public async Task<IActionResult> UpdateTags(string id, [FromBody] UpdateTagsRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Labels = request.Tags ?? [];
        await context.SaveChangesAsync();

        return Ok(new { conv.Id, conv.Labels });
    }

    [HttpPost("{id}/subscribe")]
    public async Task<IActionResult> ToggleSubscribe(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        if (conv.SubscriberIds.Contains(CurrentUserId))
            conv.SubscriberIds.Remove(CurrentUserId);
        else
            conv.SubscriberIds.Add(CurrentUserId);

        await context.SaveChangesAsync();

        await hubContext.Clients.Group(tenantProvider.TenantId).SendAsync("OnSubscriptionChanged", new
        {
            conversationId = id,
            userId = CurrentUserId,
        });

        return Ok(new { conv.Id, conv.SubscriberIds, IsSubscribed = conv.SubscriberIds.Contains(CurrentUserId) });
    }

    [HttpPost("{id}/archive")]
    public async Task<IActionResult> ArchiveConversation(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Status = MailStatus.Archived;

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Archived",
            Detail = "会话已归档",
            TenantId = tenantProvider.TenantId
        });
        await context.SaveChangesAsync();

        await hubContext.Clients.Group(tenantProvider.TenantId).SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Archived",
            actorName = CurrentUserName
        });

        return Ok(new { message = "已归档" });
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> ResolveConversation(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Status = MailStatus.Resolved;
        conv.ResolvedAt = DateTime.UtcNow;
        await slaTrackerService.MarkResolvedAsync(id);

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = conv.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Resolved",
            Detail = "会话已结案",
            TenantId = tenantProvider.TenantId
        });
        await context.SaveChangesAsync();
        await ticketService.SyncFromConversationIdAsync(id);

        await hubContext.Clients.Group(tenantProvider.TenantId).SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Resolved",
            actorName = CurrentUserName
        });

        return Ok(new { message = "已结案" });
    }

    [HttpPost("{id}/reopen")]
    public async Task<IActionResult> ReopenConversation(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.Status = MailStatus.Open;
        conv.ResolvedAt = null;

        await context.SaveChangesAsync();

        await hubContext.Clients.Group(tenantProvider.TenantId).SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Open",
            actorName = CurrentUserName
        });

        return Ok(new { message = "已重新打开" });
    }

    [HttpGet("{id}/comments")]
    public async Task<IActionResult> GetComments(string id)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var comments = await context.MailComments
            .AsNoTracking()
            .Where(c => c.ConversationId == id && c.ParentCommentId == null)
            .Include(c => c.Replies.OrderBy(r => r.CreatedAt))
            .OrderBy(c => c.CreatedAt)
            .Select(c => new
            {
                c.Id, c.Content, c.UserId, c.UserName, c.CreatedAt,
                Attachments = context.Attachments.Where(a => a.CommentId == c.Id).Select(a => new { a.Id, a.FileName, a.FileUrl, a.ContentType, a.SizeBytes }).ToList(),
                Replies = c.Replies.Select(r => new {
                    r.Id, r.Content, r.UserId, r.UserName, r.CreatedAt,
                    Attachments = context.Attachments.Where(a => a.CommentId == r.Id).Select(a => new { a.Id, a.FileName, a.FileUrl, a.ContentType, a.SizeBytes }).ToList()
                })
            })
            .ToListAsync();

        return Ok(comments);
    }

    [HttpPost("{id}/comments")]
    public async Task<IActionResult> AddComment(string id, [FromBody] AddCommentRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "评论内容不能为空" });

        var comment = new MailComment
        {
            ConversationId = id,
            ParentCommentId = request.ParentCommentId,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Content = request.Content,
            TenantId = tenantProvider.TenantId
        };
        context.MailComments.Add(comment);
        await context.SaveChangesAsync();

        if (request.AttachmentIds?.Count > 0)
        {
            await context.Attachments
                .Where(a => request.AttachmentIds.Contains(a.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(a => a.CommentId, comment.Id));
        }

        await hubContext.Clients.Group($"conversation_{id}").SendAsync("OnCommentAdded", new
        {
            conversationId = id,
            commentId = comment.Id,
            userName = CurrentUserName,
            content = comment.Content,
            createdAt = comment.CreatedAt
        });

        return Ok(new { comment.Id, comment.Content, comment.UserId, comment.UserName, comment.CreatedAt, comment.ParentCommentId });
    }

    [HttpPost("{id}/assign")]
    public async Task<IActionResult> AssignConversation(string id, [FromBody] AssignRequest request)
    {
        var denied = await DenyUnlessCanAccessAsync(id);
        if (denied != null) return denied;

        var conv = await context.Conversations.FirstOrDefaultAsync(c => c.Id == id);
        if (conv == null) return NotFound(new { message = "会话不存在" });

        conv.AssignedToUserId = request.AssignedToUserId;
        conv.AssignedToUserName = request.AssignedToUserName;
        conv.AssignedAt = DateTime.UtcNow;
        conv.Status = MailStatus.Assigned;

        await context.SaveChangesAsync();
        await ticketService.SyncFromConversationIdAsync(id);

        await hubContext.Clients.All.SendAsync("OnConversationGlobalStatusChanged", new
        {
            conversationId = id,
            status = "Assigned",
            actorName = CurrentUserName
        });

        return Ok(new { message = "已指派" });
    }
}

public record SnoozeRequest(DateTime Until);
public record ReplyRequest(string TextBody, string? ToAddress, string? Cc);
public record UnifiedReplyRequest(
    string TextContent,
    string? HtmlContent,
    string? Cc,
    string? Bcc,
    List<string>? AttachmentIds);
public record AssignRequest(string AssignedToUserId, string? AssignedToUserName);
public record CommentRequest(string Content);
public record AddCommentRequest(string Content, string? ParentCommentId, List<string>? AttachmentIds);
public record UpdateTagsRequest(List<string> Tags);

file static class MailAddressParser
{
    public static string ExtractDisplayName(string? fromAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress)) return "Unknown";
        var idx = fromAddress.IndexOf('<');
        if (idx > 0)
        {
            var name = fromAddress[..idx].Trim().Trim('"');
            return string.IsNullOrWhiteSpace(name) ? ExtractEmail(fromAddress).Split('@')[0] : name;
        }
        return ExtractEmail(fromAddress).Split('@')[0];
    }

    public static string ExtractEmail(string? fromAddress)
    {
        if (string.IsNullOrWhiteSpace(fromAddress)) return "";
        var start = fromAddress.IndexOf('<');
        var end = fromAddress.IndexOf('>');
        if (start >= 0 && end > start)
            return fromAddress[(start + 1)..end].Trim();
        return fromAddress.Trim();
    }
}

file static class SnippetHelper
{
    public static string GetSnippet(ConversationMessage? msg)
    {
        if (msg == null) return string.Empty;
        var body = !string.IsNullOrWhiteSpace(msg.TextBody) ? msg.TextBody : msg.HtmlBody;
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        var clean = System.Text.RegularExpressions.Regex.Replace(body, "<[^>]+>", "");
        return clean.Length > 60 ? clean[..60] + "..." : clean;
    }
}
