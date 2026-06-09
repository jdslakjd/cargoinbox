using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class RulesEngineProcessor(CargoInboxContext context, ExpressionRuleEvaluator evaluator)
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
                context.MailComments.Add(new MailComment { ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Content = $"自动触发规则 [{rule.Name}]：追加标签 [{rule.ActionValue}]" });
                context.ActivityLogs.Add(new ActivityLog { ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Action = "RuleTriggered", Detail = $"标签: {rule.ActionValue}" });
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
                context.MailComments.Add(new MailComment { ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Content = $"自动触发规则 [{rule.Name}]：自动指派给 [{assignee.DisplayName}]" });
                context.ActivityLogs.Add(new ActivityLog { ConversationId = conversation.Id, UserId = "system-rules", UserName = "规则引擎", Action = "RuleTriggered", Detail = $"指派: {assignee.DisplayName}" });
            }
        }
        await context.SaveChangesAsync();
    }
}
