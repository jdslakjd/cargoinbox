using System.ComponentModel.DataAnnotations.Schema;

namespace CargoInbox.Core.Entities;

public enum UserRole { User, Admin }

public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public UserRole Role { get; set; } = UserRole.User;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? DefaultSignatureId { get; set; }
    public string Timezone { get; set; } = "Asia/Shanghai";
    public string Locale { get; set; } = "zh-CN";
    public List<UserMailConfig> MailConfigs { get; set; } = [];

    public Tenant? Tenant { get; set; }
}

public class UserMailConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string? SharedInboxId { get; set; }
    public string EmailAddress { get; set; } = string.Empty;
    public string ImapHost { get; set; } = string.Empty;
    public int ImapPort { get; set; } = 993;
    public bool ImapUseSsl { get; set; } = true;
    public string SmtpHost { get; set; } = string.Empty;
    public int SmtpPort { get; set; } = 465;
    public bool SmtpUseSsl { get; set; } = true;
    public MailProviderType ProviderType { get; set; } = MailProviderType.Custom_IMAP_SMTP;
    public string EncryptedAppPassword { get; set; } = string.Empty;
    public int ConsecutiveFailureCount { get; set; }
    public bool IsSuspended { get; set; }
    public DateTime? SuspendedUntil { get; set; }
    public uint LastSyncUid { get; set; }
}

public class Customer
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? CompanyName { get; set; }
    public string? ContactName { get; set; }
    public string? Country { get; set; }
    public string LifecycleStatus { get; set; } = "Lead";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class Mail
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string MailConfigId { get; set; } = string.Empty;
    public string? ProviderMessageId { get; set; }
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string TextBody { get; set; } = string.Empty;
    public string HtmlBody { get; set; } = string.Empty;
    public DateTime DateTime { get; set; } = DateTime.UtcNow;
    public bool IsRead { get; set; }
    public bool IsStarred { get; set; }
    public List<string> Labels { get; set; } = ["INBOX"];

    [Column(TypeName = "vector(2048)")]
    public Pgvector.Vector? Embedding { get; set; }

    public MailStatus Status { get; set; } = MailStatus.Open;
    public string? AssignedToUserId { get; set; }
    public DateTime? AssignedAt { get; set; }

    public List<MailComment> Comments { get; set; } = [];
}
