using CargoInbox.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CargoInbox.Api.Controllers;

[AllowAnonymous]
[ApiController]
[Route("api/livechat")]
public class LiveChatPublicController(LiveChatService liveChatService) : ControllerBase
{
    [HttpGet("widget/{publicKey}")]
    public async Task<IActionResult> GetWidgetConfig(string publicKey)
    {
        var widget = await liveChatService.GetWidgetByPublicKeyAsync(publicKey);
        if (widget == null) return NotFound(new { message = "Widget not found" });
        return Ok(LiveChatService.ToPublicConfig(widget));
    }

    [HttpPost("widget/{publicKey}/session")]
    public async Task<IActionResult> StartSession(string publicKey, [FromBody] LiveChatSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.VisitorId))
            return BadRequest(new { message = "visitorId is required" });

        var result = await liveChatService.StartOrResumeSessionAsync(
            publicKey,
            request.VisitorId.Trim(),
            request.VisitorName,
            request.VisitorEmail);

        if (result == null) return NotFound(new { message = "Widget not found or disabled" });

        return Ok(new
        {
            sessionToken = result.SessionToken,
            conversationId = result.ConversationId,
            contactId = result.ContactId,
            widget = result.Widget,
            messages = result.Messages
        });
    }

    [HttpPost("sessions/{sessionToken}/messages")]
    public async Task<IActionResult> SendMessage(string sessionToken, [FromBody] LiveChatMessageRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            return BadRequest(new { message = "message is required" });

        var message = await liveChatService.SendVisitorMessageAsync(sessionToken, request.Message);
        if (message == null) return NotFound(new { message = "Session not found" });
        return Ok(message);
    }

    [HttpGet("sessions/{sessionToken}/messages")]
    public async Task<IActionResult> GetMessages(string sessionToken, [FromQuery] DateTime? since)
    {
        var messages = await liveChatService.GetMessagesAsync(sessionToken, since);
        return Ok(new { data = messages });
    }
}

public record LiveChatSessionRequest(string VisitorId, string? VisitorName, string? VisitorEmail);
public record LiveChatMessageRequest(string Message);
