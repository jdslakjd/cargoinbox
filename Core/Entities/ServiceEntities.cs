namespace CargoInbox.Core.Entities;

public enum TicketStatus { New = 0, Open = 1, Pending = 2, Resolved = 3, Closed = 4 }

public enum TicketPriority { Low = 0, Normal = 1, High = 2, Urgent = 3 }

/// <summary>Service desk ticket linked to an inbox conversation (erxes frontline ticket model).</summary>
public class ServiceTicket
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public int Number { get; set; }
    public string ConversationId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public MessageChannel Channel { get; set; } = MessageChannel.Email;
    public TicketStatus Status { get; set; } = TicketStatus.New;
    public TicketPriority Priority { get; set; } = TicketPriority.Normal;
    public string? ContactId { get; set; }
    public string? AssignedToUserId { get; set; }
    public string? AssignedToUserName { get; set; }
    public string? SharedInboxId { get; set; }
    public string? TeamGroupId { get; set; }
    public DateTime? FirstResponseAt { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ClosedAt { get; set; }
    public bool IsSlaBreached { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Conversation? Conversation { get; set; }
    public Contact? Contact { get; set; }
}

/// <summary>Round-robin cursor per routing scope (team group, shared inbox, or tenant default).</summary>
public class RoutingQueueCursor
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ScopeKey { get; set; } = "default";
    public string? LastAssignedUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
