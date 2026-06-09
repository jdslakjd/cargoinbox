namespace CargoInbox.Core.Entities;

public class MessageTemplate
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public bool IsShared { get; set; }
    public int UsageCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class Draft
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string CcAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class UserSignature
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = "默认签名";
    public string HtmlContent { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class InboxNotification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string? LinkUrl { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ActivityLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string? ConversationId { get; set; }
    public string? MailId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SlaPolicy
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int FirstResponseMinutes { get; set; } = 60;
    public int ResolutionMinutes { get; set; } = 480;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class TeamGroup
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamId { get; set; } = string.Empty;
    public List<string> MemberUserIds { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class SavedView
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string FilterJson { get; set; } = "{}";
    public bool IsPinned { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum SlaBreachStatus { None, Warning, Breached }

public enum NotificationType
{
    Mention,
    Assignment,
    Comment,
    SlaBreach,
    Reminder,
    System
}
