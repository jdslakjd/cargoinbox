using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class RulesEngineProcessor(
    CargoInboxContext context,
    ExpressionRuleEvaluator evaluator,
    RoundRobinAssignmentService roundRobin,
    TicketService ticketService)
{
    public async Task ProcessConversationAsync(Conversation conversation, ConversationMessage latestMessage)
    {
        var activeRules = await context.AutomationRules
            .Include(r => r.Conditions)
            .Where(r => r.IsActive && r.TenantId == conversation.TenantId)
            .ToListAsync();

        foreach (var rule in activeRules)
        {
            var predicate = evaluator.BuildPredicate(rule);
            if (!predicate(latestMessage)) continue;

            if (rule.ActionType == "Label" && !conversation.Labels.Contains(rule.ActionValue))
            {
                conversation.Labels.Add(rule.ActionValue);
                context.MailComments.Add(new MailComment { TenantId = conversation.TenantId, ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Content = $"自动触发规则 [{rule.Name}]：追加标签 [{rule.ActionValue}]" });
                context.ActivityLogs.Add(new ActivityLog { TenantId = conversation.TenantId, ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Action = "RuleTriggered", Detail = $"标签: {rule.ActionValue}" });
            }
            else if (rule.ActionType == "Assign" && string.IsNullOrEmpty(conversation.AssignedToUserId))
            {
                var assignee = await context.Users.AsNoTracking()
                    .FirstOrDefaultAsync(u =>
                        u.Id == rule.ActionValue
                        || u.Username == rule.ActionValue
                        || u.Email == rule.ActionValue);
                if (assignee == null) continue;

                conversation.Status = MailStatus.Assigned;
                conversation.AssignedToUserId = assignee.Id;
                conversation.AssignedToUserName = assignee.DisplayName;
                conversation.AssignedAt = DateTime.UtcNow;
                context.MailComments.Add(new MailComment { TenantId = conversation.TenantId, ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Content = $"自动触发规则 [{rule.Name}]：自动指派给 [{assignee.DisplayName}]" });
                context.ActivityLogs.Add(new ActivityLog { TenantId = conversation.TenantId, ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Action = "RuleTriggered", Detail = $"指派: {assignee.DisplayName}" });
            }
            else if (rule.ActionType == "RoundRobin" && string.IsNullOrEmpty(conversation.AssignedToUserId))
            {
                string? teamGroupId = null;
                string? sharedInboxId = conversation.SharedInboxId;
                if (!string.IsNullOrEmpty(rule.ActionValue))
                {
                    if (rule.ActionValue.StartsWith("team:", StringComparison.OrdinalIgnoreCase))
                        teamGroupId = rule.ActionValue["team:".Length..];
                    else if (rule.ActionValue.StartsWith("inbox:", StringComparison.OrdinalIgnoreCase))
                        sharedInboxId = rule.ActionValue["inbox:".Length..];
                    else
                        teamGroupId = rule.ActionValue;
                }

                var assigned = await roundRobin.TryAssignConversationAsync(
                    conversation,
                    teamGroupId,
                    sharedInboxId,
                    "system-rules",
                    "规则引擎");
                if (assigned)
                {
                    context.ActivityLogs.Add(new ActivityLog
                    {
                        TenantId = conversation.TenantId,
                        ConversationId = conversation.Id,
                        UserId = "system-rules",
                        UserName = "规则引擎",
                        Action = "RuleTriggered",
                        Detail = $"Round-robin: {rule.Name}"
                    });
                }
            }
        }

        await ticketService.EnsureForConversationAsync(conversation, tryAutoAssign: true);
        await context.SaveChangesAsync();
    }
}
