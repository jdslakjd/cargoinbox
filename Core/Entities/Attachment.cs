namespace CargoInbox.Core.Entities;

public class Attachment
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string MessageId { get; set; } = string.Empty;
    public string? CommentId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }
    public string FileUrl { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string StorageProvider { get; set; } = "S3";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public MailComment? Comment { get; set; }
}
