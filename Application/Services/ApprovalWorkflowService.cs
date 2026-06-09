using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CargoInbox.Application.Services;

public class ApprovalWorkflowService(IServiceScopeFactory scopeFactory)
{
    public async Task<bool> RequiresApprovalAsync(string userId)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
        return await context.Set<ApprovalRule>()
            .AnyAsync(r => r.RequesterUserId == userId && r.IsActive);
    }

    public async Task<string> SubmitForApprovalAsync(string conversationId, string userId, string subject, string htmlBody, string? textBody, string? ccAddress)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var rule = await context.Set<ApprovalRule>()
            .FirstOrDefaultAsync(r => r.RequesterUserId == userId && r.IsActive);

        if (rule == null) throw new InvalidOperationException("未找到审批规则");

        var approval = new MessageApproval
        {
            ConversationId = conversationId,
            RequesterUserId = userId,
            ApproverUserId = rule.ApproverUserId,
            Subject = subject,
            HtmlBody = htmlBody,
            TextBody = textBody,
            CcAddress = ccAddress
        };

        context.Set<MessageApproval>().Add(approval);
        await context.SaveChangesAsync();
        return approval.Id;
    }

    public async Task ApproveAsync(string approvalId, string approverUserId)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var approval = await context.Set<MessageApproval>().FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval == null || approval.Status != ApprovalStatus.Pending) return;

        approval.Status = ApprovalStatus.Approved;
        approval.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        var mailSendService = scope.ServiceProvider.GetRequiredService<MailSendService>();
        await mailSendService.SendFromConversationAsync(
            approval.ConversationId, approval.RequesterUserId,
            approval.Subject, approval.HtmlBody, approval.TextBody, approval.CcAddress,
            new MailSendOptions { BypassApproval = true });
    }

    public async Task RejectAsync(string approvalId, string approverUserId, string reason)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var approval = await context.Set<MessageApproval>().FirstOrDefaultAsync(a => a.Id == approvalId);
        if (approval == null) return;

        approval.Status = ApprovalStatus.Rejected;
        approval.RejectionReason = reason;
        approval.ResolvedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
    }
}
