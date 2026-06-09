using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using CargoInbox.Core.Entities;
using CargoInbox.Application.Services;

namespace CargoInbox.Infrastructure.Data;

public class CargoInboxContext : DbContext
{
    private readonly ITenantProvider _tenantProvider;

    public CargoInboxContext(DbContextOptions<CargoInboxContext> options, ITenantProvider tenantProvider)
        : base(options)
    {
        _tenantProvider = tenantProvider;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<User> Users => Set<User>();
    public DbSet<UserMailConfig> UserMailConfigs => Set<UserMailConfig>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Mail> Mails => Set<Mail>();
    public DbSet<MailComment> MailComments => Set<MailComment>();
    public DbSet<Conversation> Conversations => Set<Conversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<Contact> Contacts => Set<Contact>();
    public DbSet<MessageTemplate> MessageTemplates => Set<MessageTemplate>();
    public DbSet<Draft> Drafts => Set<Draft>();
    public DbSet<UserSignature> UserSignatures => Set<UserSignature>();
    public DbSet<InboxNotification> Notifications => Set<InboxNotification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<SlaPolicy> SlaPolicies => Set<SlaPolicy>();
    public DbSet<TeamGroup> TeamGroups => Set<TeamGroup>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<RuleCondition> RuleConditions => Set<RuleCondition>();
    public DbSet<MessageApproval> MessageApprovals => Set<MessageApproval>();
    public DbSet<ApprovalRule> ApprovalRules => Set<ApprovalRule>();
    public DbSet<SharedInbox> SharedInboxes => Set<SharedInbox>();
    public DbSet<UserInboxPermission> UserInboxPermissions => Set<UserInboxPermission>();
    public DbSet<ConversationDraft> ConversationDrafts => Set<ConversationDraft>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<EmailSequence> EmailSequences => Set<EmailSequence>();
    public DbSet<SequenceStep> SequenceSteps => Set<SequenceStep>();
    public DbSet<SequenceExecution> SequenceExecutions => Set<SequenceExecution>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<ShopifyOrder> ShopifyOrders => Set<ShopifyOrder>();
    public DbSet<SequenceTracker> SequenceTrackers => Set<SequenceTracker>();
    public DbSet<CallLog> CallLogs => Set<CallLog>();
    public DbSet<OAuthToken> OAuthTokens => Set<OAuthToken>();
    public DbSet<ScheduledMessage> ScheduledMessages => Set<ScheduledMessage>();
    public DbSet<TenantChannelConfig> TenantChannelConfigs => Set<TenantChannelConfig>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.HasKey(e => e.Id);
        });

        modelBuilder.Entity<Tenant>().HasData(
            new Tenant { Id = "default", Name = "默认租户", Domain = "cargoinbox.cn", IsActive = true }
        );

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.TenantId, e.Username }).IsUnique();
            entity.HasOne(e => e.Tenant).WithMany(t => t.Users).HasForeignKey(e => e.TenantId);
            entity.HasQueryFilter(u => u.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<UserMailConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Contact>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Mail>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Labels).HasColumnType("text[]");
            entity.HasIndex(e => e.Labels).HasMethod("gin");
            // Vector index omitted — pgvector caps at 2000 dims, model uses 2048
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedToUserId);
            entity.HasQueryFilter(m => m.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<MailComment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Content).IsRequired();
            entity.HasOne(e => e.Mail)
                  .WithMany(m => m.Comments)
                  .HasForeignKey(e => e.MailId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Conversation)
                  .WithMany(c => c.Comments)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Parent)
                  .WithMany(e => e.Replies)
                  .HasForeignKey(e => e.ParentCommentId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Conversation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.AssignedToUserId);
            entity.HasIndex(e => e.SnoozedUntil);
            entity.HasIndex(e => e.SlaBreachAt);
            entity.HasOne(c => c.Customer)
                  .WithMany()
                  .HasForeignKey(c => c.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(c => c.Contact)
                  .WithMany(ct => ct.Conversations)
                  .HasForeignKey(c => c.ContactId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(c => c.SharedInbox)
                  .WithMany()
                  .HasForeignKey(c => c.SharedInboxId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne<Conversation>()
                  .WithMany(c => c.Messages)
                  .HasForeignKey(e => e.ConversationId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Vector index omitted — pgvector caps at 2000 dims, model uses 2048
            entity.HasQueryFilter(m => m.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<AutomationRule>(entity => {
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(r => r.TenantId == _tenantProvider.TenantId);
        });
        modelBuilder.Entity<AutomationRule>().HasData(
            new AutomationRule { Id = "rule-1", TenantId = "default", Name = "紧急邮件自动打标", IsActive = true, ConditionKeyword = "Urgent", ActionType = "Label", ActionValue = "🔥高优先级", ConditionsJson = "[]" },
            new AutomationRule { Id = "rule-2", TenantId = "default", Name = "报价咨询自动指派", IsActive = true, ConditionKeyword = "报价", ActionType = "Assign", ActionValue = "kate", ConditionsJson = "[]" }
        );

        RegisterFeatureEntities(modelBuilder);
        base.OnModelCreating(modelBuilder);
    }

    private void RegisterFeatureEntities(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MessageTemplate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TeamId);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(t => t.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Draft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(d => d.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<UserSignature>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(s => s.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<InboxNotification>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.IsRead });
            entity.HasQueryFilter(n => n.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ActivityLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasQueryFilter(a => a.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SlaPolicy>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(p => p.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<TeamGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TeamId);
            entity.HasQueryFilter(g => g.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SavedView>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.UserId);
            entity.HasQueryFilter(v => v.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<RuleCondition>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Rule)
                  .WithMany(r => r.Conditions)
                  .HasForeignKey(e => e.RuleId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<MessageApproval>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.RequesterUserId);
            entity.HasQueryFilter(a => a.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ApprovalRule>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.RequesterUserId).IsUnique();
            entity.HasQueryFilter(r => r.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SharedInbox>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EmailAddress).IsUnique();
            entity.HasQueryFilter(i => i.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<UserInboxPermission>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.SharedInboxId }).IsUnique();
            entity.HasQueryFilter(p => p.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ConversationDraft>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ConversationId).IsUnique();
            entity.HasQueryFilter(d => d.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<Attachment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.MessageId);
            entity.HasIndex(e => e.CommentId);
            entity.HasOne(e => e.Comment)
                  .WithMany()
                  .HasForeignKey(e => e.CommentId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasQueryFilter(a => a.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<EmailSequence>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasQueryFilter(s => s.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SequenceStep>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Sequence)
                  .WithMany(s => s.Steps)
                  .HasForeignKey(e => e.SequenceId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasQueryFilter(s => s.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SequenceExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.NextStepAt);
            entity.HasQueryFilter(e => e.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<CalendarEvent>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.OrganizerUserId, e.StartTimeUtc });
            entity.HasQueryFilter(e => e.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ShopifyOrder>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.CustomerEmail);
            entity.HasQueryFilter(o => o.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<SequenceTracker>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.ContactId, e.IsCompleted });
            entity.HasQueryFilter(t => t.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<CallLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ContactId);
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<OAuthToken>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.UserId, e.Provider });
            entity.HasQueryFilter(o => o.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<ScheduledMessage>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.IsSent, e.ScheduledAtUtc });
            entity.HasQueryFilter(m => m.TenantId == _tenantProvider.TenantId);
        });

        modelBuilder.Entity<TenantChannelConfig>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.TenantId).IsUnique();
            entity.HasQueryFilter(c => c.TenantId == _tenantProvider.TenantId);
        });
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added);

        foreach (var entry in entries)
        {
            var prop = entry.Entity.GetType().GetProperty("TenantId");
            if (prop != null && prop.CanWrite && prop.GetValue(entry.Entity) is string val && string.IsNullOrEmpty(val))
            {
                prop.SetValue(entry.Entity, _tenantProvider.TenantId);
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
