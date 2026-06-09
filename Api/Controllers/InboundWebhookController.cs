using System.Text;
using System.Text.Json;
using CargoInbox.Api.Helpers;
using CargoInbox.Api.Hubs;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[ApiController]
[Route("api/webhooks/inbound")]
public class InboundWebhookController(
    CargoInboxContext context,
    ContactCaptureService contactService,
    InboundConversationService inboundConversationService,
    IHubContext<CollaborationHub> hubContext,
    IConfiguration configuration) : ControllerBase
{
    private string? MetaAppSecret => configuration["Webhooks:MetaAppSecret"];
    private string? MetaVerifyToken => configuration["Webhooks:MetaVerifyToken"];

    [HttpGet("whatsapp")]
    public IActionResult VerifyWhatsApp(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe"
            && !string.IsNullOrEmpty(MetaVerifyToken)
            && verifyToken == MetaVerifyToken
            && !string.IsNullOrEmpty(challenge))
        {
            return Content(challenge, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("whatsapp")]
    public async Task<IActionResult> ReceiveWhatsApp()
    {
        try
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(MetaAppSecret))
            {
                var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (!MetaWebhookHelper.VerifySignature(rawBody, MetaAppSecret, signature))
                    return Unauthorized(new { error = "Invalid webhook signature" });
            }

            using var doc = JsonDocument.Parse(rawBody);
            var payload = doc.RootElement;

            if (!payload.TryGetProperty("entry", out var entryArray) || entryArray.GetArrayLength() == 0) return BadRequest();
            var changes = entryArray[0].GetProperty("changes");
            if (changes.GetArrayLength() == 0) return BadRequest();
            var value = changes[0].GetProperty("value");

            if (!value.TryGetProperty("messages", out var messagesArray) || messagesArray.GetArrayLength() == 0)
                return Ok(new { status = "ignored_hook_event" });

            var messageObj = messagesArray[0];
            string fromPhone = messageObj.GetProperty("from").GetString() ?? string.Empty;

            string textBody = "Formated Media/Unsupported Message";
            if (messageObj.GetProperty("type").GetString() == "text")
            {
                textBody = messageObj.GetProperty("text").GetProperty("body").GetString() ?? string.Empty;
            }

            if (string.IsNullOrEmpty(fromPhone)) return BadRequest();

            var tenantId = await ResolveWhatsAppTenantIdAsync(value);

            string contactId = await contactService.GetOrCreateContactByPhoneAsync(fromPhone, tenantId);

            var (conversation, message, isNew) = await inboundConversationService.AppendOrCreateAsync(
                new InboundConversationService.InboundMessageRequest(
                    tenantId,
                    contactId,
                    MessageChannel.WhatsApp,
                    fromPhone,
                    "whatsapp@cargoinbox.cn",
                    "WhatsApp Instant Message",
                    textBody,
                    $"<p>{textBody}</p>",
                    "whatsapp-channel",
                    $"WhatsApp 往来: {fromPhone}"));

            context.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tenantId,
                ConversationId = conversation.Id,
                UserId = "system-webhook",
                UserName = "WhatsApp网关",
                Action = isNew ? "InboundWhatsApp" : "InboundWhatsAppReply",
                Detail = isNew
                    ? $"WhatsApp 消息入站: {fromPhone}"
                    : $"WhatsApp 续聊消息: {fromPhone}"
            });
            await context.SaveChangesAsync();

            if (isNew)
            {
                await hubContext.Clients.All.SendAsync("OnGlobalNewConversationReceived", new
                {
                    conversationId = conversation.Id,
                    channel = "WhatsApp",
                    snippet = textBody.Length > 120 ? textBody[..120] : textBody
                });
            }
            else
            {
                await hubContext.Clients.Group($"conversation_{conversation.Id}").SendAsync("OnNewMessageReceived", new
                {
                    conversationId = conversation.Id,
                    messageSnippet = textBody.Length > 120 ? textBody[..120] : textBody,
                    senderName = fromPhone
                });
            }

            return Ok(new { success = true, conversationId = conversation.Id, isNewThread = isNew });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("tiktok-ad")]
    public async Task<IActionResult> ReceiveTikTokLead([FromBody] JsonElement payload)
    {
        try
        {
            string leadId = payload.TryGetProperty("lead_id", out var lId) ? lId.GetString() ?? Guid.NewGuid().ToString("N") : Guid.NewGuid().ToString("N");
            string email = payload.TryGetProperty("email", out var e) ? e.GetString() ?? "" : "";
            string phone = payload.TryGetProperty("phone", out var ph) ? ph.GetString() ?? "" : "";
            string companyName = payload.TryGetProperty("company_name", out var cName) ? cName.GetString() ?? "TikTok 留资企业" : "TikTok 留资企业";

            if (string.IsNullOrEmpty(email) && string.IsNullOrEmpty(phone)) return BadRequest("Email or phone required");

            var tenantId = "default";
            string contactId;
            if (!string.IsNullOrEmpty(email))
                contactId = await contactService.GetOrCreateContactAsync(email, tenantId);
            else
                contactId = await contactService.GetOrCreateContactByPhoneAsync(phone, tenantId);

            var contact = await context.Contacts.IgnoreQueryFilters().FirstAsync(c => c.Id == contactId);
            if (string.IsNullOrEmpty(contact.TikTokLeadId))
            {
                contact.TikTokLeadId = leadId;
                contact.LeadSource = "TikTok_AD";
                if (!string.IsNullOrEmpty(phone)) contact.Phone = phone;
                if (!string.IsNullOrEmpty(companyName)) contact.Name = companyName;
                await context.SaveChangesAsync();
            }

            var body = $"客户在 TikTok 广告活动中提交了留资。公司: {companyName}, 电话: {phone}, 邮箱: {email}";
            var (conversation, _, isNew) = await inboundConversationService.AppendOrCreateAsync(
                new InboundConversationService.InboundMessageRequest(
                    tenantId,
                    contactId,
                    MessageChannel.TikTok,
                    email,
                    "tiktok@cargoinbox.cn",
                    $"TikTok 留资: {companyName}",
                    body,
                    $"<p>{body}</p>",
                    "tiktok-channel",
                    $"🎯 TikTok 广告表单: {companyName}"));

            context.ActivityLogs.Add(new ActivityLog
            {
                TenantId = tenantId,
                ConversationId = conversation.Id,
                UserId = "system-webhook",
                UserName = "TikTok网关",
                Action = "InboundLeadCapture",
                Detail = $"TikTok LeadId: {leadId}"
            });
            await context.SaveChangesAsync();

            if (isNew)
            {
                await hubContext.Clients.All.SendAsync("OnGlobalNewConversationReceived", new
                {
                    conversationId = conversation.Id,
                    channel = "TikTok",
                    snippet = companyName
                });
            }

            return Ok(new { message = "TikTok Lead sync closed successfully", conversationId = conversation.Id });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpGet("facebook-page")]
    public IActionResult VerifyFacebookPage(
        [FromQuery(Name = "hub.mode")] string? mode,
        [FromQuery(Name = "hub.verify_token")] string? verifyToken,
        [FromQuery(Name = "hub.challenge")] string? challenge)
    {
        if (mode == "subscribe"
            && !string.IsNullOrEmpty(MetaVerifyToken)
            && verifyToken == MetaVerifyToken
            && !string.IsNullOrEmpty(challenge))
        {
            return Content(challenge, "text/plain");
        }

        return Unauthorized();
    }

    [HttpPost("facebook-page")]
    public async Task<IActionResult> ReceiveFacebookMessenger()
    {
        try
        {
            Request.EnableBuffering();
            using var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true);
            var rawBody = await reader.ReadToEndAsync();
            Request.Body.Position = 0;

            if (!string.IsNullOrEmpty(MetaAppSecret))
            {
                var signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
                if (!MetaWebhookHelper.VerifySignature(rawBody, MetaAppSecret, signature))
                    return Unauthorized(new { error = "Invalid webhook signature" });
            }

            using var doc = JsonDocument.Parse(rawBody);
            var payload = doc.RootElement;

            if (!payload.TryGetProperty("object", out var obj) || obj.GetString() != "page") return BadRequest();
            var entry = payload.GetProperty("entry")[0];
            if (!entry.TryGetProperty("messaging", out var messagingArray) || messagingArray.GetArrayLength() == 0) return BadRequest();
            var messaging = messagingArray[0];

            if (!messaging.TryGetProperty("sender", out var sender) || !messaging.TryGetProperty("message", out var msgObj)) return BadRequest();

            string senderPsid = sender.GetProperty("id").GetString() ?? "";
            string messageText = msgObj.TryGetProperty("text", out var textEl)
                ? textEl.GetString() ?? ""
                : "Unsupported message type";

            if (string.IsNullOrEmpty(senderPsid)) return BadRequest();

            var pageId = entry.TryGetProperty("id", out var pageIdEl) ? pageIdEl.GetString() : null;
            var tenantId = await ResolveFacebookTenantIdAsync(pageId);

            var contact = await context.Contacts.FirstOrDefaultAsync(c => c.FacebookPsid == senderPsid);
            if (contact == null)
            {
                contact = new Contact
                {
                    TenantId = tenantId,
                    Name = $"FB 访客 {senderPsid[..Math.Min(6, senderPsid.Length)]}",
                    FacebookPsid = senderPsid,
                    LeadSource = "Facebook_Page"
                };
                context.Contacts.Add(contact);
                await context.SaveChangesAsync();
            }
            else if (string.IsNullOrEmpty(contact.TenantId))
            {
                contact.TenantId = tenantId;
                await context.SaveChangesAsync();
            }

            var (conversation, message, isNew) = await inboundConversationService.AppendOrCreateAsync(
                new InboundConversationService.InboundMessageRequest(
                    tenantId,
                    contact.Id,
                    MessageChannel.Facebook,
                    senderPsid,
                    "facebook@cargoinbox.cn",
                    "Facebook Messenger Private Chat",
                    messageText,
                    $"<p>{messageText}</p>",
                    "facebook-channel",
                    $"💬 Facebook 实时私信: {contact.Name}"));

            if (isNew)
            {
                await hubContext.Clients.All.SendAsync("OnGlobalNewConversationReceived", new
                {
                    conversationId = conversation.Id,
                    channel = "Facebook",
                    snippet = messageText.Length > 120 ? messageText[..120] : messageText
                });
            }
            else
            {
                await hubContext.Clients.Group($"conversation_{conversation.Id}").SendAsync("OnNewMessageReceived", new
                {
                    conversationId = conversation.Id,
                    messageSnippet = messageText.Length > 120 ? messageText[..120] : messageText,
                    senderName = contact.Name
                });
            }

            return Ok(new { success = true, conversationId = conversation.Id, isNewThread = isNew });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    private async Task<string> ResolveWhatsAppTenantIdAsync(JsonElement value)
    {
        string? phoneNumberId = null;
        if (value.TryGetProperty("metadata", out var metadata)
            && metadata.TryGetProperty("phone_number_id", out var pn))
        {
            phoneNumberId = pn.GetString();
        }

        if (!string.IsNullOrEmpty(phoneNumberId))
        {
            var cfg = await context.TenantChannelConfigs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.WhatsAppPhoneNumberId == phoneNumberId);
            if (cfg != null) return cfg.TenantId;
        }

        return "default";
    }

    private async Task<string> ResolveFacebookTenantIdAsync(string? pageId)
    {
        if (!string.IsNullOrEmpty(pageId))
        {
            var cfg = await context.TenantChannelConfigs
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.FacebookPageId == pageId);
            if (cfg != null) return cfg.TenantId;
        }

        return "default";
    }
}
