using System.Security.Claims;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class InboxPermissionService(CargoInboxContext context)
{
    public static bool IsAdmin(ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.Role) == UserRole.Admin.ToString();

    public async Task<HashSet<string>> GetAllowedSharedInboxIdsAsync(string userId, string tenantId, bool isAdmin)
    {
        if (isAdmin)
        {
            return (await context.SharedInboxes
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId && s.IsActive)
                .Select(s => s.Id)
                .ToListAsync()).ToHashSet();
        }

        var fromMail = await context.UserMailConfigs
            .AsNoTracking()
            .Where(c => c.UserId == userId && c.SharedInboxId != null)
            .Select(c => c.SharedInboxId!)
            .ToListAsync();

        var fromGrant = await context.UserInboxPermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .Select(p => p.SharedInboxId)
            .ToListAsync();

        return fromMail.Concat(fromGrant).ToHashSet();
    }

    public IQueryable<Conversation> ApplyConversationAccessFilter(
        IQueryable<Conversation> query,
        string userId,
        bool isAdmin,
        IReadOnlyCollection<string> allowedInboxIds)
    {
        if (isAdmin) return query;

        var inboxIds = allowedInboxIds.ToList();
        return query.Where(c =>
            c.SharedInboxId == null
            || inboxIds.Contains(c.SharedInboxId)
            || c.AssignedToUserId == userId
            || c.SubscriberIds.Contains(userId));
    }

    public async Task<bool> CanAccessSharedInboxAsync(
        string userId, string tenantId, bool isAdmin, string sharedInboxId)
    {
        if (isAdmin) return true;
        var allowed = await GetAllowedSharedInboxIdsAsync(userId, tenantId, false);
        return allowed.Contains(sharedInboxId);
    }

    public async Task<bool> CanAccessConversationAsync(
        string userId, string tenantId, bool isAdmin, string conversationId)
    {
        if (isAdmin) return true;

        var conv = await context.Conversations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == conversationId);
        if (conv == null) return false;

        return await CanAccessConversationAsync(userId, tenantId, false, conv);
    }

    public async Task<bool> CanAccessConversationAsync(
        string userId, string tenantId, bool isAdmin, Conversation conv)
    {
        if (isAdmin) return true;
        if (conv.AssignedToUserId == userId) return true;
        if (conv.SubscriberIds.Contains(userId)) return true;
        if (string.IsNullOrEmpty(conv.SharedInboxId)) return true;

        var allowed = await GetAllowedSharedInboxIdsAsync(userId, tenantId, false);
        return allowed.Contains(conv.SharedInboxId);
    }

    public async Task<List<string>> GetGrantedInboxIdsAsync(string userId, string tenantId) =>
        await context.UserInboxPermissions
            .AsNoTracking()
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .Select(p => p.SharedInboxId)
            .ToListAsync();

    public async Task SetUserInboxPermissionsAsync(string userId, string tenantId, IEnumerable<string> sharedInboxIds)
    {
        var inboxList = sharedInboxIds.Distinct().ToList();
        var validIds = await context.SharedInboxes
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId && s.IsActive && inboxList.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        var existing = await context.UserInboxPermissions
            .Where(p => p.UserId == userId && p.TenantId == tenantId)
            .ToListAsync();
        context.UserInboxPermissions.RemoveRange(existing);

        foreach (var inboxId in validIds)
        {
            context.UserInboxPermissions.Add(new UserInboxPermission
            {
                TenantId = tenantId,
                UserId = userId,
                SharedInboxId = inboxId
            });
        }

        await context.SaveChangesAsync();
    }
}
