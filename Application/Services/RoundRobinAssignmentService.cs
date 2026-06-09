using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class RoundRobinAssignmentService(CargoInboxContext context)
{
    public async Task<User?> PickNextAssigneeAsync(
        string tenantId,
        string? teamGroupId = null,
        string? sharedInboxId = null,
        CancellationToken cancellationToken = default)
    {
        var candidateIds = await GetCandidateUserIdsAsync(tenantId, teamGroupId, sharedInboxId, cancellationToken);
        if (candidateIds.Count == 0) return null;

        var scopeKey = BuildScopeKey(teamGroupId, sharedInboxId);
        var cursor = await context.RoutingQueueCursors
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId && c.ScopeKey == scopeKey, cancellationToken);

        if (cursor == null)
        {
            cursor = new RoutingQueueCursor { TenantId = tenantId, ScopeKey = scopeKey };
            context.RoutingQueueCursors.Add(cursor);
            await context.SaveChangesAsync(cancellationToken);
        }

        var startIndex = 0;
        if (!string.IsNullOrEmpty(cursor.LastAssignedUserId))
        {
            var lastIdx = candidateIds.IndexOf(cursor.LastAssignedUserId);
            if (lastIdx >= 0) startIndex = (lastIdx + 1) % candidateIds.Count;
        }

        for (var i = 0; i < candidateIds.Count; i++)
        {
            var userId = candidateIds[(startIndex + i) % candidateIds.Count];
            var user = await context.Users
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId && u.IsActive, cancellationToken);
            if (user != null)
            {
                cursor.LastAssignedUserId = user.Id;
                cursor.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync(cancellationToken);
                return user;
            }
        }

        return null;
    }

    public async Task<bool> TryAssignConversationAsync(
        Conversation conversation,
        string? teamGroupId = null,
        string? sharedInboxId = null,
        string actorUserId = "system-routing",
        string actorUserName = "自动路由",
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrEmpty(conversation.AssignedToUserId)) return false;

        var assignee = await PickNextAssigneeAsync(
            conversation.TenantId,
            teamGroupId,
            sharedInboxId ?? conversation.SharedInboxId,
            cancellationToken);

        if (assignee == null) return false;

        conversation.Status = MailStatus.Assigned;
        conversation.AssignedToUserId = assignee.Id;
        conversation.AssignedToUserName = assignee.DisplayName;
        conversation.AssignedAt = DateTime.UtcNow;

        context.MailComments.Add(new MailComment
        {
            TenantId = conversation.TenantId,
            ConversationId = conversation.Id,
            UserId = actorUserId,
            UserName = actorUserName,
            Content = $"Round-robin 自动指派给 [{assignee.DisplayName}]"
        });
        context.ActivityLogs.Add(new ActivityLog
        {
            TenantId = conversation.TenantId,
            ConversationId = conversation.Id,
            UserId = actorUserId,
            UserName = actorUserName,
            Action = "RoundRobinAssign",
            Detail = assignee.DisplayName
        });

        return true;
    }

    private async Task<List<string>> GetCandidateUserIdsAsync(
        string tenantId,
        string? teamGroupId,
        string? sharedInboxId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrEmpty(teamGroupId))
        {
            var group = await context.TeamGroups
                .IgnoreQueryFilters()
                .AsNoTracking()
                .FirstOrDefaultAsync(g => g.Id == teamGroupId && g.TenantId == tenantId, cancellationToken);
            if (group?.MemberUserIds.Count > 0)
                return group.MemberUserIds.Distinct().ToList();
        }

        if (!string.IsNullOrEmpty(sharedInboxId))
        {
            var inboxUserIds = await context.UserInboxPermissions
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(p => p.TenantId == tenantId && p.SharedInboxId == sharedInboxId)
                .Select(p => p.UserId)
                .Distinct()
                .ToListAsync(cancellationToken);
            if (inboxUserIds.Count > 0) return inboxUserIds;
        }

        return await context.Users
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => u.Id)
            .ToListAsync(cancellationToken);
    }

    public static string BuildScopeKey(string? teamGroupId, string? sharedInboxId)
    {
        if (!string.IsNullOrEmpty(teamGroupId)) return $"team:{teamGroupId}";
        if (!string.IsNullOrEmpty(sharedInboxId)) return $"inbox:{sharedInboxId}";
        return "default";
    }
}
