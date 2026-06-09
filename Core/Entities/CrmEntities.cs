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
    ProfileUpdate = 8,
    DealCreated = 9,
    DealStageChange = 10,
    DealClosed = 11
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

public enum DealStatus { Open = 0, Won = 1, Lost = 2 }

public class Pipeline
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Name { get; set; } = "Sales Pipeline";
    public bool IsDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<PipelineStage> Stages { get; set; } = [];
    public List<Deal> Deals { get; set; } = [];
}

public class PipelineStage
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string PipelineId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public int WinProbability { get; set; }

    public Pipeline? Pipeline { get; set; }
}

public class Deal
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "USD";
    public string PipelineId { get; set; } = string.Empty;
    public string StageId { get; set; } = string.Empty;
    public string? ContactId { get; set; }
    public string? CompanyId { get; set; }
    public string? ConversationId { get; set; }
    public string? OwnerUserId { get; set; }
    public string? OwnerUserName { get; set; }
    public DateTime? ExpectedCloseDate { get; set; }
    public DealStatus Status { get; set; } = DealStatus.Open;
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public Pipeline? Pipeline { get; set; }
    public PipelineStage? Stage { get; set; }
    public Contact? Contact { get; set; }
    public Company? Company { get; set; }
}
