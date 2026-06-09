namespace CargoInbox.Core.Entities;

public class Tenant
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = string.Empty;
    public string? Domain { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<User> Users { get; set; } = [];
    public List<Conversation> Conversations { get; set; } = [];
    public List<Contact> Contacts { get; set; } = [];
}
