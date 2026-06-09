using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class NotificationService(CargoInboxContext context)
{
    public async Task NotifyMentionAsync(string mentionedUserId, string fromUserName, string conversationId, string snippet)
    {
        context.Notifications.Add(new InboxNotification
        {
            UserId = mentionedUserId,
            Type = NotificationType.Mention.ToString(),
            Title = $"{fromUserName} 在会话中 @了你",
            Body = snippet.Length > 120 ? snippet[..120] + "..." : snippet,
            LinkUrl = $"/conversations/{conversationId}"
        });
        await context.SaveChangesAsync();
    }

    public async Task NotifyAssignmentAsync(string assignedUserId, string fromUserName, string conversationId)
    {
        context.Notifications.Add(new InboxNotification
        {
            UserId = assignedUserId,
            Type = NotificationType.Assignment.ToString(),
            Title = $"{fromUserName} 将会话指派给了你",
            Body = "请及时处理",
            LinkUrl = $"/conversations/{conversationId}"
        });
        await context.SaveChangesAsync();
    }

    public async Task NotifyCommentAsync(string conversationUserId, string commenterName, string conversationId)
    {
        context.Notifications.Add(new InboxNotification
        {
            UserId = conversationUserId,
            Type = NotificationType.Comment.ToString(),
            Title = $"{commenterName} 评论了你的会话",
            Body = "有人在你的会话中发表了新评论",
            LinkUrl = $"/conversations/{conversationId}"
        });
        await context.SaveChangesAsync();
    }

    public async Task NotifySlaBreachAsync(string userId, string conversationId)
    {
        context.Notifications.Add(new InboxNotification
        {
            UserId = userId,
            Type = NotificationType.SlaBreach.ToString(),
            Title = "SLA 超时告警",
            Body = "您有会话超过响应时限，请立即处理",
            LinkUrl = $"/conversations/{conversationId}"
        });
        await context.SaveChangesAsync();
    }

    public async Task<List<InboxNotification>> GetUnreadNotificationsAsync(string userId)
    {
        return await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .Take(50)
            .ToListAsync();
    }
}
