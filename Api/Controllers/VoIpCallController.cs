using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/voip")]
public class VoIpCallController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Agent";

    [HttpPost("logs")]
    public async Task<IActionResult> RecordCallLog([FromBody] CallLogRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { error = "电话号码不能为空" });
        if (string.IsNullOrWhiteSpace(request.ConversationId))
            return BadRequest(new { error = "关联会话ID不能为空" });

        var callLog = new CallLog
        {
            TenantId = tenantProvider.TenantId,
            UserId = CurrentUserId,
            ContactId = request.ContactId,
            ConversationId = request.ConversationId,
            PhoneNumber = request.PhoneNumber,
            DurationSeconds = request.DurationSeconds,
            RecordingUrl = request.RecordingUrl ?? "",
            AudioToTextTranscript = request.AudioToTextTranscript ?? ""
        };
        context.Set<CallLog>().Add(callLog);

        var systemMessage = new ConversationMessage
        {
            ConversationId = request.ConversationId,
            FromAddress = "voip@cargoinbox.cn",
            ToAddress = request.PhoneNumber,
            Subject = "跨国电话随访录音档案",
            TextBody = $"📞 通话时长: {request.DurationSeconds}秒。\nWhisper智能转写文本: {request.AudioToTextTranscript}",
            HtmlBody = $"<p>📞 通话时长: <b>{request.DurationSeconds}秒</b></p><p>转写: {request.AudioToTextTranscript}</p>",
            DateTime = DateTime.UtcNow,
            Type = MessageType.SystemNotification,
            TenantId = tenantProvider.TenantId
        };
        context.ConversationMessages.Add(systemMessage);

        context.ActivityLogs.Add(new ActivityLog
        {
            ConversationId = request.ConversationId,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "VoIpCallRecorded",
            Detail = $"跨国通话 {request.PhoneNumber}，时长 {request.DurationSeconds}秒",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { success = true, callLogId = callLog.Id, messageId = systemMessage.Id });
    }

    [HttpGet("logs/{contactId}")]
    public async Task<IActionResult> GetContactCallLogs(string contactId)
    {
        var logs = await context.Set<CallLog>()
            .AsNoTracking()
            .Where(c => c.ContactId == contactId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(50)
            .ToListAsync();
        return Ok(logs);
    }
}

public record CallLogRequest(
    string ConversationId, string ContactId, string PhoneNumber,
    int DurationSeconds, string? RecordingUrl, string? AudioToTextTranscript
);
