using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/send")]
public class MailSendController(
    MailSendService mailSendService,
    Infrastructure.Data.CargoInboxContext context,
    ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";

    [HttpPost]
    public async Task<IActionResult> SendMail([FromBody] SendMailRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.ConversationId))
        {
            if (string.IsNullOrWhiteSpace(request.Subject))
                return BadRequest(new { message = "邮件主题不能为空" });

            await mailSendService.SendFromConversationAsync(
                request.ConversationId, CurrentUserId, request.Subject,
                request.HtmlBody, request.TextBody, request.CcAddress,
                new MailSendOptions { BccAddress = request.BccAddress, AttachmentIds = request.AttachmentIds });
            return Ok(new { message = "邮件已发送" });
        }

        if (!string.IsNullOrWhiteSpace(request.ToEmail))
        {
            await SendByEmailAsync(request.ToEmail, request.Subject, request.HtmlBody, request.TextBody, request.CcAddress, request.BccAddress, request.AttachmentIds);
            return Ok(new { message = "邮件已发送" });
        }

        return BadRequest(new { message = "ConversationId 或 ToEmail 必须提供一项" });
    }

    private async Task SendByEmailAsync(string toEmail, string subject, string htmlBody, string? textBody, string? ccAddress, string? bccAddress, List<string>? attachmentIds)
    {
        var contact = await context.Contacts.FirstOrDefaultAsync(c => c.Email == toEmail);
        if (contact == null)
        {
            contact = new Contact { Name = toEmail, Email = toEmail, TenantId = tenantProvider.TenantId };
            context.Contacts.Add(contact);
            await context.SaveChangesAsync();
        }

        var conversation = await context.Conversations
            .FirstOrDefaultAsync(c => c.ContactId == contact.Id && c.Status != MailStatus.Archived);

        if (conversation == null)
        {
            conversation = new Conversation
            {
                Title = subject,
                ContactId = contact.Id,
                Channel = MessageChannel.Email,
                TenantId = tenantProvider.TenantId
            };
            context.Conversations.Add(conversation);
            await context.SaveChangesAsync();
        }

        await mailSendService.SendFromConversationAsync(
            conversation.Id, CurrentUserId, subject, htmlBody, textBody, ccAddress,
            new MailSendOptions
            {
                ToAddress = toEmail,
                CcAddress = ccAddress,
                BccAddress = bccAddress,
                AttachmentIds = attachmentIds
            });
    }

    [HttpPost("draft")]
    public async Task<IActionResult> SaveDraft([FromBody] SaveDraftRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
            return BadRequest(new { message = "会话ID不能为空" });

        var existing = await context.Drafts.FirstOrDefaultAsync(d => d.ConversationId == request.ConversationId && d.UserId == CurrentUserId);
        if (existing != null)
        {
            existing.ToAddress = request.ToAddress;
            existing.CcAddress = request.CcAddress ?? "";
            existing.Subject = request.Subject;
            existing.HtmlBody = request.HtmlBody;
            existing.TextBody = request.TextBody ?? "";
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            context.Drafts.Add(new Draft
            {
                ConversationId = request.ConversationId,
                UserId = CurrentUserId,
                ToAddress = request.ToAddress,
                CcAddress = request.CcAddress ?? "",
                Subject = request.Subject,
                HtmlBody = request.HtmlBody,
                TextBody = request.TextBody ?? ""
            });
        }
        await context.SaveChangesAsync();
        return Ok(new { message = "草稿已保存" });
    }

    [HttpGet("draft/{conversationId}")]
    public async Task<IActionResult> GetDraft(string conversationId)
    {
        var draft = await context.Drafts.AsNoTracking().FirstOrDefaultAsync(d => d.ConversationId == conversationId && d.UserId == CurrentUserId);
        return draft == null ? NotFound() : Ok(draft);
    }
}

public record SendMailRequest(string? ConversationId, string Subject, string HtmlBody, string? ToEmail, string? TextBody, string? CcAddress, string? BccAddress, List<string>? AttachmentIds);
public record SaveDraftRequest(string ConversationId, string ToAddress, string? CcAddress, string Subject, string HtmlBody, string? TextBody);
