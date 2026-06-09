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
public class ContactsController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
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
                || (c.Company != null && EF.Functions.Like(c.Company, $"%{q}%")));
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(c => c.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return Ok(new { data = items, page, pageSize, total });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetContact(string id)
    {
        var contact = await context.Contacts.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();
        return Ok(contact);
    }

    [HttpPost]
    public async Task<IActionResult> CreateContact([FromBody] Contact contact)
    {
        contact.Id = Guid.NewGuid().ToString("N");
        contact.TenantId = tenantProvider.TenantId;
        contact.CreatedAt = DateTime.UtcNow;
        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
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

    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateContact(string id, [FromBody] Contact updated)
    {
        var contact = await context.Contacts.FirstOrDefaultAsync(c => c.Id == id);
        if (contact == null) return NotFound();

        contact.Name = updated.Name;
        contact.Email = updated.Email;
        contact.Phone = updated.Phone;
        contact.Company = updated.Company;
        contact.Notes = updated.Notes;
        contact.LifecycleStatus = updated.LifecycleStatus;

        await context.SaveChangesAsync();
        return Ok(contact);
    }
}
