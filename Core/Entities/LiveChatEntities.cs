namespace CargoInbox.Core.Entities;

public class LiveChatWidget
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    /// <summary>Public embed key (erxes integration-style widget id).</summary>
    public string PublicKey { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "Website Chat";
    public string WelcomeMessage { get; set; } = "Hi! How can we help you today?";
    public string OfflineMessage { get; set; } = "We are currently offline. Leave a message and we will get back to you.";
    public string PrimaryColor { get; set; } = "#4f46e5";
    public string Position { get; set; } = "right";
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class LiveChatSession
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string WidgetId { get; set; } = string.Empty;
    public string VisitorId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string? VisitorName { get; set; }
    public string? VisitorEmail { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActiveAt { get; set; } = DateTime.UtcNow;

    public LiveChatWidget? Widget { get; set; }
    public Contact? Contact { get; set; }
    public Conversation? Conversation { get; set; }
}
