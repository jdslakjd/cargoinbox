using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/contacts")]
public class ContactsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    CrmActivityService crmActivity,
    CrmTimelineService timelineService,
    CrmCustomFieldService customFieldService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetContacts(
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 30)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 5, 100);

        var query = context.Contacts.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            query = query.Where(c =>
                EF.Functions.Like(c.Name, $"%{q}%")
                || (c.Email != null && EF.Functions.Like(c.Email, $"%{q}%"))
                || (c.Phone != null && EF.Functions.Like(c.Phone, $"%{q}%"))
                || (c.Company != null && EF.Functions.Like(c.Company, $"%{q}%"))
                || (c.LinkedCompany != null && EF.Functions.Like(c.LinkedCompany.Name, $"%{q}%")));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(c => new
            {
                c.Id,
                c.TenantId,
                c.Name,
                c.Email,
                c.Phone,
                c.Company,
                c.CompanyId,
                CompanyName = c.LinkedCompany != null ? c.LinkedCompany.Name : c.Company,
                c.OwnerUserId,
                c.OwnerUserName,
                c.Tags,
                c.LeadSource,
                c.LifecycleStatus,
                c.CreatedAt,
                c.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { data = items, page, pageSize, total });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContact(string id)
    {
        var contact = await context.Contacts
            .AsNoTracking()
            .Include(c => c.LinkedCompany)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        var customFields = await customFieldService.GetValuesAsync(CrmEntityType.Contact, id);

        return Ok(new
        {
            contact.Id,
            contact.TenantId,
            contact.Name,
            contact.Email,
            contact.Phone,
            contact.Company,
            contact.CompanyId,
            CompanyName = contact.LinkedCompany?.Name ?? contact.Company,
            contact.OwnerUserId,
            contact.OwnerUserName,
            contact.Tags,
            contact.Notes,
            contact.LeadSource,
            contact.LifecycleStatus,
            contact.CreatedAt,
            contact.UpdatedAt,
            customFields,
            LinkedCompany = contact.LinkedCompany == null ? null : new
            {
                contact.LinkedCompany.Id,
                contact.LinkedCompany.Name,
                contact.LinkedCompany.Domain
            }
        });
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] ContactCreateRequest request)
    {
        var (userId, userName) = CrmActivityService.ResolveActor(User);
        var contact = new Contact
        {
            TenantId = tenantProvider.TenantId,
            Name = string.IsNullOrWhiteSpace(request.Name) ? "Unknown" : request.Name.Trim(),
            Email = request.Email?.Trim(),
            Phone = request.Phone?.Trim(),
            Company = request.Company?.Trim(),
            Notes = request.Notes,
            LeadSource = request.LeadSource?.Trim(),
            OwnerUserId = request.OwnerUserId,
            OwnerUserName = request.OwnerUserName,
            Tags = request.Tags ?? [],
            LifecycleStatus = request.LifecycleStatus ?? ContactStatus.NewLead
        };

        await ApplyCompanyLinkAsync(contact, request.CompanyId, request.Company?.Trim());

        context.Contacts.Add(contact);
        await context.SaveChangesAsync();

        if (request.CustomFields != null)
            await customFieldService.SaveValuesAsync(CrmEntityType.Contact, contact.Id, request.CustomFields, tenantProvider.TenantId);

        await crmActivity.LogAsync(
            CrmActivityType.ProfileUpdate,
            $"Contact created: {contact.Name}",
            contactId: contact.Id,
            companyId: contact.CompanyId,
            userId: userId,
            userName: userName);

        return Ok(contact);
    }

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateContact(string id, [FromBody] ContactUpdateRequest request)
    {
        var contact = await context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        var (userId, userName) = CrmActivityService.ResolveActor(User);
        var changes = new List<string>();

        if (contact.Name != request.Name) changes.Add($"name → {request.Name}");
        if (contact.LifecycleStatus != request.LifecycleStatus)
            changes.Add($"lifecycle → {request.LifecycleStatus}");

        var oldOwner = contact.OwnerUserName;
        contact.Name = request.Name.Trim();
        contact.Email = request.Email?.Trim();
        contact.Phone = request.Phone?.Trim();
        contact.Notes = request.Notes;
        contact.LeadSource = request.LeadSource?.Trim();
        contact.LifecycleStatus = request.LifecycleStatus;
        contact.OwnerUserId = request.OwnerUserId;
        contact.OwnerUserName = request.OwnerUserName;
        contact.Tags = request.Tags ?? [];
        contact.UpdatedAt = DateTime.UtcNow;

        if (oldOwner != contact.OwnerUserName && !string.IsNullOrEmpty(contact.OwnerUserName))
        {
            await crmActivity.LogAsync(
                CrmActivityType.OwnerChange,
                $"Owner set to {contact.OwnerUserName}",
                contactId: contact.Id,
                companyId: contact.CompanyId,
                userId: userId,
                userName: userName);
        }

        await ApplyCompanyLinkAsync(contact, request.CompanyId, request.Company?.Trim());

        await context.SaveChangesAsync();

        if (changes.Count > 0)
        {
            await crmActivity.LogAsync(
                CrmActivityType.ProfileUpdate,
                "Contact profile updated",
                string.Join("; ", changes),
                contact.Id,
                contact.CompanyId,
                userId: userId,
                userName: userName);
        }

        if (request.CustomFields != null)
            await customFieldService.SaveValuesAsync(CrmEntityType.Contact, contact.Id, request.CustomFields, tenantProvider.TenantId);

        return Ok(contact);
    }

    [HttpGet("{id}/conversations")]
    public async Task<IActionResult> GetContactConversations(string id, [FromQuery] int limit = 20)
    {
        limit = Math.Clamp(limit, 1, 50);
        var contact = await context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        var conversations = await context.Conversations
            .AsNoTracking()
            .Where(c => c.ContactId == id)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(limit)
            .Select(c => new
            {
                c.Id,
                c.Title,
                c.Channel,
                c.Status,
                c.LastMessageAt,
                c.AssignedToUserName
            })
            .ToListAsync();

        return Ok(new { data = conversations, contactId = id });
    }

    [HttpGet("{id}/timeline")]
    public async Task<IActionResult> GetContactTimeline(string id, [FromQuery] int limit = 50)
    {
        var exists = await context.Contacts.AsNoTracking().AnyAsync(c => c.Id == id);
        if (!exists) return NotFound();
        var timeline = await timelineService.BuildContactTimelineAsync(id, limit);
        return Ok(new { data = timeline });
    }

    [HttpPost("{id}/notes")]
    public async Task<IActionResult> AddContactNote(string id, [FromBody] AddNoteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Body))
            return BadRequest(new { message = "Note body is required" });

        var contact = await context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        var (userId, userName) = CrmActivityService.ResolveActor(User);
        var title = string.IsNullOrWhiteSpace(request.Title) ? "Note added" : request.Title.Trim();

        var activity = await crmActivity.LogAsync(
            CrmActivityType.Note,
            title,
            request.Body.Trim(),
            contactId: id,
            companyId: contact.CompanyId,
            userId: userId,
            userName: userName);

        return Ok(activity);
    }

    private async Task ApplyCompanyLinkAsync(Contact contact, string? companyId, string? companyName)
    {
        if (!string.IsNullOrWhiteSpace(companyId))
        {
            var linked = await context.Companies.FirstOrDefaultAsync(c => c.Id == companyId);
            if (linked != null)
            {
                contact.CompanyId = linked.Id;
                contact.Company = linked.Name;
            }
            return;
        }

        if (string.IsNullOrWhiteSpace(companyName))
        {
            contact.Company = null;
            contact.CompanyId = null;
            return;
        }

        contact.Company = companyName;
        var existing = await context.Companies
            .FirstOrDefaultAsync(c => c.Name == companyName);

        if (existing == null)
        {
            existing = new Company
            {
                TenantId = tenantProvider.TenantId,
                Name = companyName,
                OwnerUserId = contact.OwnerUserId,
                OwnerUserName = contact.OwnerUserName
            };
            context.Companies.Add(existing);
            await context.SaveChangesAsync();
        }

        contact.CompanyId = existing.Id;
    }

    public record ContactCreateRequest(
        string Name,
        string? Email,
        string? Phone,
        string? Company,
        string? CompanyId,
        string? Notes,
        string? LeadSource,
        string? OwnerUserId,
        string? OwnerUserName,
        List<string>? Tags,
        ContactStatus? LifecycleStatus,
        Dictionary<string, string>? CustomFields);

    public record ContactUpdateRequest(
        string Name,
        string? Email,
        string? Phone,
        string? Company,
        string? CompanyId,
        string? Notes,
        string? LeadSource,
        string? OwnerUserId,
        string? OwnerUserName,
        List<string>? Tags,
        ContactStatus LifecycleStatus,
        Dictionary<string, string>? CustomFields);

    public record AddNoteRequest(string Body, string? Title);
}
