using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/companies")]
public class CompaniesController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    CrmActivityService crmActivity,
    CrmTimelineService timelineService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetCompanies(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = context.Companies.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{q}%")
                || (c.Domain != null && EF.Functions.Like(c.Domain, $"%{q}%"))
                || (c.Industry != null && EF.Functions.Like(c.Industry, $"%{q}%")));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Domain,
                c.Industry,
                c.OwnerUserId,
                c.OwnerUserName,
                c.Tags,
                c.Notes,
                c.CreatedAt,
                c.UpdatedAt,
                ContactCount = context.Contacts.Count(ct => ct.CompanyId == c.Id)
            })
            .ToListAsync();

        return Ok(new { data = items, page, pageSize, total });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetCompany(string id)
    {
        var company = await context.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        var contacts = await context.Contacts
            .AsNoTracking()
            .Where(c => c.CompanyId == id)
            .OrderByDescending(c => c.UpdatedAt)
            .Select(c => new
            {
                c.Id,
                c.Name,
                c.Email,
                c.Phone,
                c.LifecycleStatus,
                c.OwnerUserName,
                c.UpdatedAt
            })
            .Take(50)
            .ToListAsync();

        return Ok(new { company, contacts });
    }

    [HttpPost]
    public async Task<IActionResult> CreateCompany([FromBody] CompanyRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            return BadRequest(new { message = "Company name is required" });

        var (userId, userName) = CrmActivityService.ResolveActor(User);
        var company = new Company
        {
            TenantId = tenantProvider.TenantId,
            Name = request.Name.Trim(),
            Domain = request.Domain?.Trim(),
            Industry = request.Industry?.Trim(),
            Notes = request.Notes,
            OwnerUserId = request.OwnerUserId,
            OwnerUserName = request.OwnerUserName,
            Tags = request.Tags ?? []
        };

        context.Companies.Add(company);
        await context.SaveChangesAsync();

        await crmActivity.LogAsync(
            CrmActivityType.ProfileUpdate,
            $"Company created: {company.Name}",
            companyId: company.Id,
            userId: userId,
            userName: userName);

        return Ok(company);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateCompany(string id, [FromBody] CompanyRequest request)
    {
        var company = await context.Companies.FirstOrDefaultAsync(c => c.Id == id);
        if (company == null) return NotFound();

        var (userId, userName) = CrmActivityService.ResolveActor(User);

        company.Name = request.Name.Trim();
        company.Domain = request.Domain?.Trim();
        company.Industry = request.Industry?.Trim();
        company.Notes = request.Notes;
        company.OwnerUserId = request.OwnerUserId;
        company.OwnerUserName = request.OwnerUserName;
        company.Tags = request.Tags ?? [];
        company.UpdatedAt = DateTime.UtcNow;

        await context.SaveChangesAsync();

        await crmActivity.LogAsync(
            CrmActivityType.ProfileUpdate,
            $"Company updated: {company.Name}",
            companyId: company.Id,
            userId: userId,
            userName: userName);

        return Ok(company);
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetCompanyTimeline(string id, [FromQuery] int limit = 50)
    {
        var company = await context.Companies.AsNoTracking().AnyAsync(c => c.Id == id);
        if (!company) return NotFound();
        var timeline = await timelineService.BuildCompanyTimelineAsync(id, limit);
        return Ok(new { data = timeline });
    }

    public record CompanyRequest(
        string Name,
        string? Domain,
        string? Industry,
        string? Notes,
        string? OwnerUserId,
        string? OwnerUserName,
        List<string>? Tags);
}
