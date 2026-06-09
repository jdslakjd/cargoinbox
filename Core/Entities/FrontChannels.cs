namespace CargoInbox.Core.Entities;

public class SharedInbox
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public bool SmtpUseSsl { get; set; } = true;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; }
    public bool ImapUseSsl { get; set; } = true;
    public MailProviderType ProviderType { get; set; } = MailProviderType.Custom_IMAP_SMTP;
    public string EncryptedPassword { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public uint LastSyncUid { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class UserInboxPermission
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string SharedInboxId { get; set; } = string.Empty;
}

public class ConversationDraft
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string ConversationId { get; set; } = string.Empty;
    public string CreatedByUserId { get; set; } = string.Empty;
    public string CreatedByUserName { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public bool IsLockedForApproval { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
