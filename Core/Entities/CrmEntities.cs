namespace CargoInbox.Core.Entities;

public class Company
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public string? Industry { get; set; }
    public string? Notes { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerUserName { get; set; }
    public List<string> Tags { get; set; } = [];
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public List<Contact> Contacts { get; set; } = [];
}

public enum CrmActivityType
{
    Note = 0,
    LifecycleChange = 1,
    Conversation = 2,
    Meeting = 3,
    Call = 4,
    OwnerChange = 5,
    TagChange = 6,
    CompanyLink = 7,
    ProfileUpdate = 8
}

public class CrmActivity
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string? ContactId { get; set; }
    public string? CompanyId { get; set; }
    public CrmActivityType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Body { get; set; }
    public string? RelatedEntityId { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Contact? Contact { get; set; }
    public Company? Company { get; set; }
}
