using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/crm/segments")]
public class CrmSegmentsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    CrmSegmentEvaluator segmentEvaluator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetSegments()
    {
        var segments = await context.CrmSegments
            .AsNoTracking()
            .OrderByDescending(s => s.UpdatedAt)
            .ToListAsync();

        var result = new List<object>();
        foreach (var segment in segments)
        {
            var count = await segmentEvaluator.CountContactMembersAsync(context, segment);
            result.Add(new
            {
                segment.Id,
                segment.Name,
                segment.Description,
                segment.FilterJson,
                segment.CreatedAt,
                segment.UpdatedAt,
                MemberCount = count
            });
        }

        return Ok(new { data = result });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetSegment(string id)
    {
        var segment = await context.CrmSegments.AsNoTracking().FirstOrDefaultAsync(s => s.Id == id);
        if (segment == null) return NotFound();

        var members = await segmentEvaluator.GetContactMembersAsync(context, segment, 100);
        var count = await segmentEvaluator.CountContactMembersAsync(context, segment);

        return Ok(new
        {
            segment,
            memberCount = count,
            members = members.Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.LifecycleStatus,
                c.OwnerUserName,
                c.Tags
            })
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateSegment([FromBody] SegmentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Segment name is required" });

        var segment = new CrmSegment
        {
            TenantId = tenantProvider.TenantId,
            Name = request.Name.Trim(),
            Description = request.Description,
            FilterJson = string.IsNullOrWhiteSpace(request.FilterJson)
                ? "{\"match\":\"all\",\"rules\":[]}"
                : request.FilterJson
        };

        context.CrmSegments.Add(segment);
        await context.SaveChangesAsync();
        return Ok(segment);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateSegment(string id, [FromBody] SegmentRequest request)
    {
        var segment = await context.CrmSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment == null) return NotFound();

        segment.Name = request.Name.Trim();
        segment.Description = request.Description;
        if (!string.IsNullOrWhiteSpace(request.FilterJson))
            segment.FilterJson = request.FilterJson;
        segment.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();
        return Ok(segment);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteSegment(string id)
    {
        var segment = await context.CrmSegments.FirstOrDefaultAsync(s => s.Id == id);
        if (segment == null) return NotFound();
        context.CrmSegments.Remove(segment);
        await context.SaveChangesAsync();
        return Ok(new { success = true });
    }

    public record SegmentRequest(string Name, string? Description, string? FilterJson);
}
