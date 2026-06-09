using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class CrmCustomFieldService(CargoInboxContext context)
{
    public async Task<Dictionary<string, string>> GetValuesAsync(CrmEntityType entityType, string entityId, CancellationToken ct = default)
    {
        var definitions = await context.CrmFieldDefinitions
            .AsNoTracking()
            .Where(f => f.EntityType == entityType && f.IsActive)
            .ToListAsync(ct);

        if (definitions.Count == 0) return [];

        var defIds = definitions.Select(d => d.Id).ToList();
        var values = await context.CrmFieldValues
            .AsNoTracking()
            .Where(v => v.EntityId == entityId && defIds.Contains(v.FieldDefinitionId))
            .ToListAsync(ct);

        return definitions.ToDictionary(
            d => d.FieldKey,
            d => values.FirstOrDefault(v => v.FieldDefinitionId == d.Id)?.Value ?? string.Empty);
    }

    public async Task SaveValuesAsync(
        CrmEntityType entityType,
        string entityId,
        Dictionary<string, string> values,
        string tenantId,
        CancellationToken ct = default)
    {
        if (values.Count == 0) return;

        var definitions = await context.CrmFieldDefinitions
            .Where(f => f.EntityType == entityType && f.IsActive)
            .ToListAsync(ct);

        foreach (var def in definitions)
        {
            if (!values.TryGetValue(def.FieldKey, out var val)) continue;

            var existing = await context.CrmFieldValues
                .FirstOrDefaultAsync(v => v.FieldDefinitionId == def.Id && v.EntityId == entityId, ct);

            if (existing == null)
            {
                context.CrmFieldValues.Add(new CrmFieldValue
                {
                    TenantId = tenantId,
                    FieldDefinitionId = def.Id,
                    EntityId = entityId,
                    Value = val ?? string.Empty
                });
            }
            else
            {
                existing.Value = val ?? string.Empty;
            }
        }

        await context.SaveChangesAsync(ct);
    }
}
