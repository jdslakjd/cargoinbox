using System.ComponentModel.DataAnnotations.Schema;

namespace CargoInbox.Core.Entities;

public enum MessageChannel { Email = 0, WhatsApp = 1, LiveChat = 2, Facebook = 3, TikTok = 4 }

public enum MessageType { Email, InstantMessage, SystemNotification }

public enum MailStatus { Open = 0, Assigned = 1, Archived = 2, Snoozed = 3, Resolved = 4, Trash = 5, Spam = 6 }

public class Conversation
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string UserId { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = "未命名会话";
    public MessageChannel Channel { get; set; } = MessageChannel.Email;
    public MailStatus Status { get; set; } = MailStatus.Open;
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public DateTime? AssignedAt { get; set; }
    public DateTime LastMessageAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<string> Labels { get; set; } = [];
    public List<string> SubscriberIds { get; set; } = [];

    public DateTime? SnoozedUntil { get; set; }
    public DateTime? SlaBreachAt { get; set; }
    public DateTime? FirstRespondedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? FirstRepliedAtUtc { get; set; }
    public bool IsSlaBreached { get; set; }

    public string? CustomerId { get; set; }
    public Customer? Customer { get; set; }

    public string? ContactId { get; set; }
    public Contact? Contact { get; set; }

    public string? SharedInboxId { get; set; }
    public SharedInbox? SharedInbox { get; set; }

    public List<ConversationMessage> Messages { get; set; } = [];
    public List<MailComment> Comments { get; set; } = [];
}

public class ConversationMessage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public DateTime DateTime { get; set; }
    public bool IsRead { get; set; }
    public MessageType Type { get; set; } = MessageType.Email;

    [Column(TypeName = "vector(2048)")]
    public Pgvector.Vector? Embedding { get; set; }
}

public class MailComment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string? MailId { get; set; }
    public string? ConversationId { get; set; }
    public string? ParentCommentId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Mail? Mail { get; set; }
    public Conversation? Conversation { get; set; }
    public MailComment? Parent { get; set; }
    public List<MailComment> Replies { get; set; } = [];
}

public class AutomationRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public string ConditionKeyword { get; set; } = string.Empty;
    public string ActionType { get; set; } = string.Empty;
    public string ActionValue { get; set; } = string.Empty;
    public string ConditionsJson { get; set; } = "[]";
    public List<RuleCondition> Conditions { get; set; } = [];
}
