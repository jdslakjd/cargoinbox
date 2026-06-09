using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/pipelines")]
public class PipelinesController(CargoInboxContext context, ITenantProvider tenantProvider, PipelineService pipelineService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetPipelines()
    {
        await pipelineService.EnsureDefaultPipelineAsync(tenantProvider.TenantId);

        var pipelines = await context.Pipelines
            .AsNoTracking()
            .Include(p => p.Stages.OrderBy(s => s.SortOrder))
            .OrderByDescending(p => p.IsDefault)
            .ThenBy(p => p.Name)
            .ToListAsync();

        return Ok(pipelines);
    }

    [HttpGet("default")]
    public async Task<IActionResult> GetDefaultPipeline()
    {
        var pipeline = await pipelineService.EnsureDefaultPipelineAsync(tenantProvider.TenantId);
        return Ok(pipeline);
    }
}
