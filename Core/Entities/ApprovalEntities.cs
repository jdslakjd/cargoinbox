namespace CargoInbox.Core.Entities;

public enum ApprovalStatus { Pending, Approved, Rejected }

public class MessageApproval
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string RequesterUserId { get; set; } = string.Empty;
    public string ApproverUserId { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public string? TextBody { get; set; }
    public string? CcAddress { get; set; }
    public ApprovalStatus Status { get; set; } = ApprovalStatus.Pending;
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }
}

public class ApprovalRule
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string RequesterUserId { get; set; } = string.Empty;
    public string ApproverUserId { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
