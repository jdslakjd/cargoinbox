using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/fields")]
public class CrmFieldsController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetDefinitions([FromQuery] CrmEntityType? entityType)
    {
        var query = context.CrmFieldDefinitions.AsNoTracking().AsQueryable();
        if (entityType.HasValue)
            query = query.Where(f => f.EntityType == entityType.Value);

        var items = await query.OrderBy(f => f.EntityType).ThenBy(f => f.SortOrder).ToListAsync();
        return Ok(new { data = items });
    }

    [HttpPost]
    public async Task<IActionResult> CreateDefinition([FromBody] FieldDefinitionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Label) || string.IsNullOrWhiteSpace(request.FieldKey))
            return BadRequest(new { message = "Label and fieldKey are required" });

        var key = request.FieldKey.Trim().ToLowerInvariant().Replace(' ', '_');
        var exists = await context.CrmFieldDefinitions.AnyAsync(f =>
            f.EntityType == request.EntityType && f.FieldKey == key);
        if (exists) return BadRequest(new { message = "Field key already exists for this entity type" });

        var def = new CrmFieldDefinition
        {
            TenantId = tenantProvider.TenantId,
            EntityType = request.EntityType,
            FieldKey = key,
            Label = request.Label.Trim(),
            FieldType = request.FieldType,
            OptionsJson = request.OptionsJson ?? "[]",
            SortOrder = request.SortOrder
        };

        context.CrmFieldDefinitions.Add(def);
        await context.SaveChangesAsync();
        return Ok(def);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateDefinition(string id, [FromBody] FieldDefinitionRequest request)
    {
        var def = await context.CrmFieldDefinitions.FirstOrDefaultAsync(f => f.Id == id);
        if (def == null) return NotFound();

        def.Label = request.Label.Trim();
        def.FieldType = request.FieldType;
        def.OptionsJson = request.OptionsJson ?? def.OptionsJson;
        def.SortOrder = request.SortOrder;
        def.IsActive = request.IsActive;

        await context.SaveChangesAsync();
        return Ok(def);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteDefinition(string id)
    {
        var def = await context.CrmFieldDefinitions.FirstOrDefaultAsync(f => f.Id == id);
        if (def == null) return NotFound();

        var values = await context.CrmFieldValues.Where(v => v.FieldDefinitionId == id).ToListAsync();
        context.CrmFieldValues.RemoveRange(values);
        context.CrmFieldDefinitions.Remove(def);
        await context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    [HttpGet("values/{entityType}/{entityId}")]
    public async Task<IActionResult> GetValues(CrmEntityType entityType, string entityId, [FromServices] CrmCustomFieldService fieldService)
    {
        var values = await fieldService.GetValuesAsync(entityType, entityId);
        return Ok(new { data = values });
    }

    [HttpPut("values/{entityType}/{entityId}")]
    public async Task<IActionResult> SaveValues(
        CrmEntityType entityType,
        string entityId,
        [FromBody] Dictionary<string, string> values,
        [FromServices] CrmCustomFieldService fieldService)
    {
        await fieldService.SaveValuesAsync(entityType, entityId, values, tenantProvider.TenantId);
        return Ok(new { success = true });
    }

    public record FieldDefinitionRequest(
        CrmEntityType EntityType,
        string FieldKey,
        string Label,
        CrmFieldType FieldType,
        string? OptionsJson,
        int SortOrder,
        bool IsActive = true);
}
