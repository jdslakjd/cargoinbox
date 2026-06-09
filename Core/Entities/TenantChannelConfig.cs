namespace CargoInbox.Core.Entities;

public class TenantChannelConfig
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string? WhatsAppPhoneNumberId { get; set; }
    public string? WhatsAppAccessToken { get; set; }
    public string? FacebookPageAccessToken { get; set; }
    public string? FacebookPageId { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
