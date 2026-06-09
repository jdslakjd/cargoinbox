namespace CargoInbox.Core.Entities;

public class ShopifyOrder
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string CustomerEmail { get; set; } = string.Empty;
    public string OrderNumber { get; set; } = string.Empty;
    public decimal TotalPrice { get; set; }
    public string Currency { get; set; } = "USD";
    public string FulfillmentStatus { get; set; } = "Unfulfilled";
    public string? TrackingNumber { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class SequenceTracker
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string CurrentStep { get; set; } = "Day1_WelcomeEmail";
    public bool IsCompleted { get; set; }
    public DateTime NextTriggerTimeUtc { get; set; }
}

public class CallLog
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string ContactId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string PhoneNumber { get; set; } = string.Empty;
    public int DurationSeconds { get; set; }
    public string RecordingUrl { get; set; } = string.Empty;
    public string AudioToTextTranscript { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
