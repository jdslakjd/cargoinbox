namespace CargoInbox.Core.Entities;

public enum SequenceTriggerType { Manual, OnNewConversation, OnSlaBreach }

public class EmailSequence
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public SequenceTriggerType TriggerType { get; set; } = SequenceTriggerType.Manual;
    public int DelayMinutes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<SequenceStep> Steps { get; set; } = [];
}

public class SequenceStep
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string SequenceId { get; set; } = string.Empty;
    public int StepOrder { get; set; }
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public int DelayAfterPreviousMinutes { get; set; } = 1440;
    public bool IsActive { get; set; } = true;

    public EmailSequence? Sequence { get; set; }
}

public class SequenceExecution
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string SequenceId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public int CurrentStep { get; set; }
    public DateTime? NextStepAt { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
}
