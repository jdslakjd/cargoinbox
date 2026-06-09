using System.Text.Json;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class CrmSegmentFilter
{
    public string Match { get; set; } = "all";
    public List<CrmSegmentRule> Rules { get; set; } = [];
}

public class CrmSegmentRule
{
    public string Field { get; set; } = string.Empty;
    public string Operator { get; set; } = "eq";
    public string? Value { get; set; }
}

public class CrmSegmentEvaluator
{
    public static CrmSegmentFilter? ParseFilter(string? filterJson)
    {
        if (string.IsNullOrWhiteSpace(filterJson)) return new CrmSegmentFilter();
        try
        {
            return JsonSerializer.Deserialize<CrmSegmentFilter>(filterJson, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch
        {
            return new CrmSegmentFilter();
        }
    }

    public IQueryable<Contact> ApplyContactFilter(IQueryable<Contact> query, CrmSegmentFilter filter)
    {
        if (filter.Rules.Count == 0) return query;

        if (filter.Match.Equals("any", StringComparison.OrdinalIgnoreCase))
        {
            var ids = query.Select(c => c.Id).ToList();
            var matched = new HashSet<string>();
            foreach (var contact in query.AsEnumerable())
            {
                if (filter.Rules.Any(r => RuleMatchesContact(contact, r)))
                    matched.Add(contact.Id);
            }
            return query.Where(c => matched.Contains(c.Id));
        }

        foreach (var rule in filter.Rules)
        {
            query = ApplyContactRule(query, rule);
        }

        return query;
    }

    private static IQueryable<Contact> ApplyContactRule(IQueryable<Contact> query, CrmSegmentRule rule)
    {
        return rule.Field.ToLowerInvariant() switch
        {
            "lifecyclestatus" when rule.Operator == "eq" && int.TryParse(rule.Value, out var status)
                => query.Where(c => (int)c.LifecycleStatus == status),
            "lifecyclestatus" when rule.Operator == "neq" && int.TryParse(rule.Value, out var nstatus)
                => query.Where(c => (int)c.LifecycleStatus != nstatus),
            "tags" when rule.Operator == "contains" && !string.IsNullOrEmpty(rule.Value)
                => query.Where(c => c.Tags.Contains(rule.Value!)),
            "owneruserid" when rule.Operator == "eq"
                => query.Where(c => c.OwnerUserId == rule.Value),
            "leadsource" when rule.Operator == "eq"
                => query.Where(c => c.LeadSource == rule.Value),
            "companyid" when rule.Operator == "eq"
                => query.Where(c => c.CompanyId == rule.Value),
            _ => query
        };
    }

    private static bool RuleMatchesContact(Contact contact, CrmSegmentRule rule)
    {
        return rule.Field.ToLowerInvariant() switch
        {
            "lifecyclestatus" when int.TryParse(rule.Value, out var status)
                => rule.Operator == "eq"
                    ? (int)contact.LifecycleStatus == status
                    : (int)contact.LifecycleStatus != status,
            "tags" when rule.Operator == "contains"
                => !string.IsNullOrEmpty(rule.Value) && contact.Tags.Contains(rule.Value),
            "owneruserid" => contact.OwnerUserId == rule.Value,
            "leadsource" => contact.LeadSource == rule.Value,
            "companyid" => contact.CompanyId == rule.Value,
            _ => false
        };
    }

    public async Task<int> CountContactMembersAsync(CargoInboxContext context, CrmSegment segment, CancellationToken ct = default)
    {
        var filter = ParseFilter(segment.FilterJson) ?? new CrmSegmentFilter();
        var query = context.Contacts.AsNoTracking().AsQueryable();
        query = ApplyContactFilter(query, filter);
        return await query.CountAsync(ct);
    }

    public async Task<List<Contact>> GetContactMembersAsync(
        CargoInboxContext context, CrmSegment segment, int limit = 50, CancellationToken ct = default)
    {
        var filter = ParseFilter(segment.FilterJson) ?? new CrmSegmentFilter();
        var query = context.Contacts.AsNoTracking().AsQueryable();
        query = ApplyContactFilter(query, filter);
        return await query.OrderByDescending(c => c.UpdatedAt).Take(limit).ToListAsync(ct);
    }
}
