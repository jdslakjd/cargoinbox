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
[Route("api/conversations/{conversationId}/draft")]
public class DraftCollaborationController(
    CargoInboxContext context,
    IHubContext<CollaborationHub> hubContext,
    MailSendService mailSendService,
    ITenantProvider tenantProvider,
    InboxPermissionService inboxPermissionService) : ControllerBase
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

    private async Task<bool> CanApproveDraftAsync(string draftCreatorUserId)
    {
        if (draftCreatorUserId == CurrentUserId) return false;
        var user = await context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == CurrentUserId);
        if (user?.Role == UserRole.Admin) return true;
        return await context.Set<ApprovalRule>().AnyAsync(r =>
            r.RequesterUserId == draftCreatorUserId && r.ApproverUserId == CurrentUserId && r.IsActive);
    }

    [HttpGet]
    public async Task<IActionResult> GetDraft(string conversationId)
    {
        var denied = await DenyUnlessCanAccessAsync(conversationId);
        if (denied != null) return denied;

        var draft = await context.ConversationDrafts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.ConversationId == conversationId);
        if (draft == null) return NotFound(new { message = "暂无草稿" });
        return Ok(new
        {
            draft,
            canApprove = draft.IsLockedForApproval && await CanApproveDraftAsync(draft.CreatedByUserId),
            canSubmit = draft.CreatedByUserId == CurrentUserId && !draft.IsLockedForApproval
        });
    }

    [HttpPut]
    public async Task<IActionResult> SaveDraft(string conversationId, [FromBody] ConversationDraft input)
    {
        var denied = await DenyUnlessCanAccessAsync(conversationId);
        if (denied != null) return denied;

        var draft = await context.ConversationDrafts.FirstOrDefaultAsync(d => d.ConversationId == conversationId);
        if (draft != null && draft.IsLockedForApproval)
            return StatusCode(403, new { message = "该草稿目前正处于主管审批锁定状态，禁止他人修改。" });

        if (draft == null)
        {
            draft = new ConversationDraft
            {
                ConversationId = conversationId,
                CreatedByUserId = CurrentUserId,
                CreatedByUserName = CurrentUserName,
                TenantId = tenantProvider.TenantId
            };
            context.ConversationDrafts.Add(draft);
        }

        draft.Subject = input.Subject;
        draft.TextBody = input.TextBody;
        draft.HtmlBody = input.HtmlBody;
        draft.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("OnDraftContentUpdated", new { conversationId, updatedBy = CurrentUserName, draft });

        return Ok(draft);
    }

    [HttpPost("submit-approval")]
    public async Task<IActionResult> SubmitApproval(string conversationId)
    {
        var denied = await DenyUnlessCanAccessAsync(conversationId);
        if (denied != null) return denied;

        var draft = await context.ConversationDrafts.FirstOrDefaultAsync(d => d.ConversationId == conversationId);
        if (draft == null) return NotFound(new { message = "没有找到草稿，无法发起审批" });

        draft.IsLockedForApproval = true;

        context.MailComments.Add(new MailComment
        {
            ConversationId = conversationId,
            UserId = "system",
            UserName = "系统通知",
            Content = $"⚠️ 成员 {CurrentUserName} 发起了发信审批申请，该会话外发动作已被拦截锁定，等待主管审核。"
        });

        await context.SaveChangesAsync();
        await hubContext.Clients.All.SendAsync("OnGlobalNotification", $"会话 {conversationId} 触发了新的发信审批流，请主管处理。");
        return Ok(new { message = "审批流已成功发起，草稿已被安全加锁" });
    }

    [HttpPost("approve-and-send")]
    public async Task<IActionResult> ApproveAndSend(string conversationId, [FromQuery] bool isApproved)
    {
        var denied = await DenyUnlessCanAccessAsync(conversationId);
        if (denied != null) return denied;

        var draft = await context.ConversationDrafts.FirstOrDefaultAsync(d => d.ConversationId == conversationId);
        if (draft == null) return NotFound();

        if (!draft.IsLockedForApproval)
            return BadRequest(new { message = "草稿未处于审批状态" });

        if (!await CanApproveDraftAsync(draft.CreatedByUserId))
            return StatusCode(403, new { message = "无权审批该草稿" });

        if (!isApproved)
        {
            draft.IsLockedForApproval = false;
            context.MailComments.Add(new MailComment { ConversationId = conversationId, UserId = CurrentUserId, UserName = CurrentUserName, Content = "❌ 主管驳回了发信申请。草稿已解锁，请修改后重新提交。" });
            await context.SaveChangesAsync();
            return Ok(new { message = "已成功驳回并退回草稿" });
        }

        draft.ApprovedByUserId = CurrentUserId;

        await mailSendService.SendFromConversationAsync(
            conversationId, draft.CreatedByUserId, draft.Subject, draft.HtmlBody, draft.TextBody, null,
            new MailSendOptions { BypassApproval = true });

        context.ConversationDrafts.Remove(draft);
        context.MailComments.Add(new MailComment { ConversationId = conversationId, UserId = CurrentUserId, UserName = CurrentUserName, Content = "✅ 主管已批准发信申请，邮件已通过 SMTP 真实发出。" });
        await context.SaveChangesAsync();

        await hubContext.Clients.Group($"conversation_{conversationId}").SendAsync("OnDraftApprovedAndSent", conversationId);
        return Ok(new { message = "审批通过，信件已成功飞出" });
    }
}
