namespace CargoInbox.Core.Entities;

public enum ContactStatus { NewLead, Nurturing, Converted, Lost }

public class Contact
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = "未知联系人";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Company { get; set; }
    public string? CompanyId { get; set; }
    public Company? LinkedCompany { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerUserName { get; set; }
    public List<string> Tags { get; set; } = [];
    public string? Notes { get; set; }
    public string? FacebookPsid { get; set; }
    public string? TikTokLeadId { get; set; }
    public string? LeadSource { get; set; }
    public ContactStatus LifecycleStatus { get; set; } = ContactStatus.NewLead;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Conversation> Conversations { get; set; } = [];
}
