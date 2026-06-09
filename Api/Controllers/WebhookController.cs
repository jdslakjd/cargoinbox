using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoInbox.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public class WebhookController(CargoInboxContext context, LiveChatService liveChatService) : ControllerBase
{
    [HttpPost("whatsapp")]
    public async Task<IActionResult> ReceiveWhatsApp([FromBody] JsonElement payload, [FromQuery] string verify_token = "cargoinbox_whatsapp_webhook_2026")
    {
        // WhatsApp Business API verification challenge (GET)
        if (Request.Method == "GET")
        {
            var mode = Request.Query["hub.mode"].FirstOrDefault();
            var token = Request.Query["hub.verify_token"].FirstOrDefault();
            var challenge = Request.Query["hub.challenge"].FirstOrDefault();

            if (mode == "subscribe" && token == verify_token)
                return Ok(challenge ?? "");
            return Unauthorized();
        }

        // Inbound message processing
        try
        {
            if (payload.TryGetProperty("entry", out var entries) && entries.GetArrayLength() > 0)
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    if (!entry.TryGetProperty("changes", out var changes)) continue;
                    foreach (var change in changes.EnumerateArray())
                    {
                        if (!change.TryGetProperty("value", out var value)) continue;
                        if (!value.TryGetProperty("messages", out var messages)) continue;

                        foreach (var msg in messages.EnumerateArray())
                        {
                            var from = value.TryGetProperty("contacts", out var contacts) &&
                                contacts.GetArrayLength() > 0 &&
                                contacts[0].TryGetProperty("profile", out var profile) &&
                                profile.TryGetProperty("name", out var nameEl)
                                    ? nameEl.GetString() ?? "WhatsApp User"
                                    : "WhatsApp User";

                            var messageBody = msg.TryGetProperty("text", out var text) &&
                                text.TryGetProperty("body", out var bodyEl)
                                    ? bodyEl.GetString() ?? ""
                                    : "";

                            var conversation = new Conversation
                            {
                                UserId = "whatsapp-channel",
                                Title = $"WhatsApp: {(from.Length > 30 ? from[..30] + "..." : from)}",
                                Channel = MessageChannel.WhatsApp,
                                Status = MailStatus.Open,
                                LastMessageAt = DateTime.UtcNow
                            };
                            context.Conversations.Add(conversation);
                            await context.SaveChangesAsync();

                            context.ConversationMessages.Add(new ConversationMessage
                            {
                                ConversationId = conversation.Id,
                                FromAddress = from,
                                ToAddress = "whatsapp-inbound",
                                Subject = $"WhatsApp message from {from}",
                                TextBody = messageBody,
                                HtmlBody = $"<p>{messageBody}</p>",
                                DateTime = DateTime.UtcNow
                            });
                            await context.SaveChangesAsync();
                        }
                    }
                }
            }
            return Ok(new { status = "received" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = ex.Message });
        }
    }

    [HttpPost("livechat")]
    public async Task<IActionResult> ReceiveLiveChat([FromBody] LiveChatPayload payload)
    {
        var widget = await liveChatService.EnsureDefaultWidgetAsync("default");
        var visitorId = !string.IsNullOrWhiteSpace(payload.VisitorEmail)
            ? payload.VisitorEmail.Trim().ToLower()
            : Guid.NewGuid().ToString("N");

        var session = await liveChatService.StartOrResumeSessionAsync(
            widget.PublicKey, visitorId, payload.VisitorName, payload.VisitorEmail);
        if (session == null) return BadRequest(new { error = "Live chat widget disabled" });

        if (!string.IsNullOrWhiteSpace(payload.Message))
            await liveChatService.SendVisitorMessageAsync(session.SessionToken, payload.Message);

        return Ok(new
        {
            status = "received",
            conversationId = session.ConversationId,
            sessionToken = session.SessionToken
        });
    }

    [HttpGet("whatsapp")]
    public IActionResult VerifyWhatsApp([FromQuery] string? hub_mode, [FromQuery] string? hub_challenge, [FromQuery] string? hub_verify_token)
    {
        if (hub_mode == "subscribe" && hub_verify_token == "cargoinbox_whatsapp_webhook_2026")
            return Ok(hub_challenge ?? "");
        return Unauthorized();
    }
}

public record LiveChatPayload(string? VisitorName, string? VisitorEmail, string? Message);
