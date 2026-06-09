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
[Route("api/tickets")]
public class TicketsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    TicketService ticketService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> ListTickets(
        [FromQuery] TicketStatus? status,
        [FromQuery] TicketPriority? priority,
        [FromQuery] MessageChannel? channel,
        [FromQuery] string? assignedToUserId,
        [FromQuery] bool? unassigned,
        [FromQuery] bool? slaBreached,
        [FromQuery] string? q,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50)
    {
        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = context.ServiceTickets.AsNoTracking().AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);
        else
            query = query.Where(t => t.Status == TicketStatus.New
                || t.Status == TicketStatus.Open
                || t.Status == TicketStatus.Pending);

        if (priority.HasValue) query = query.Where(t => t.Priority == priority.Value);
        if (channel.HasValue) query = query.Where(t => t.Channel == channel.Value);
        if (!string.IsNullOrEmpty(assignedToUserId)) query = query.Where(t => t.AssignedToUserId == assignedToUserId);
        if (unassigned == true) query = query.Where(t => t.AssignedToUserId == null || t.AssignedToUserId == "");
        if (slaBreached == true) query = query.Where(t => t.IsSlaBreached);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(t =>
                t.Subject.ToLower().Contains(term)
                || t.Number.ToString() == term
                || (t.AssignedToUserName != null && t.AssignedToUserName.ToLower().Contains(term)));
        }

        var total = await query.CountAsync();
        var tickets = await query
            .OrderByDescending(t => t.IsSlaBreached)
            .ThenByDescending(t => t.Priority)
            .ThenByDescending(t => t.UpdatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(t => new
            {
                t.Id,
                t.Number,
                t.ConversationId,
                t.Subject,
                t.Channel,
                t.Status,
                t.Priority,
                t.ContactId,
                ContactName = t.Contact != null ? t.Contact.Name : null,
                t.AssignedToUserId,
                t.AssignedToUserName,
                t.SharedInboxId,
                t.IsSlaBreached,
                t.FirstResponseAt,
                t.Tags,
                t.CreatedAt,
                t.UpdatedAt
            })
            .ToListAsync();

        return Ok(new { data = tickets, total, page, pageSize });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetTicket(string id)
    {
        var ticket = await context.ServiceTickets
            .AsNoTracking()
            .Include(t => t.Contact)
            .FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpGet("by-conversation/{conversationId}")]
    public async Task<IActionResult> GetByConversation(string conversationId)
    {
        var ticket = await context.ServiceTickets
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.ConversationId == conversationId);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.ConversationId))
            return BadRequest(new { message = "ConversationId is required" });

        var conversation = await context.Conversations.FirstOrDefaultAsync(c => c.Id == request.ConversationId);
        if (conversation == null) return NotFound(new { message = "Conversation not found" });

        if (!string.IsNullOrWhiteSpace(request.Subject))
            conversation.Title = request.Subject.Trim();

        var ticket = await ticketService.EnsureForConversationAsync(conversation, tryAutoAssign: request.AutoAssign);

        if (request.Priority.HasValue) ticket.Priority = request.Priority.Value;
        if (request.Tags?.Count > 0) ticket.Tags = request.Tags;
        await context.SaveChangesAsync();

        return Ok(ticket);
    }

    [HttpPut("{id}/assign")]
    public async Task<IActionResult> AssignTicket(string id, [FromBody] AssignTicketRequest request)
    {
        var userName = request.AssignedToUserName;
        if (string.IsNullOrEmpty(userName))
        {
            var user = await context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == request.AssignedToUserId);
            userName = user?.DisplayName;
        }

        var ticket = await ticketService.AssignAsync(id, request.AssignedToUserId, userName);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost("{id}/resolve")]
    public async Task<IActionResult> ResolveTicket(string id)
    {
        var ticket = await ticketService.ResolveAsync(id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost("{id}/close")]
    public async Task<IActionResult> CloseTicket(string id)
    {
        var ticket = await ticketService.CloseAsync(id);
        if (ticket == null) return NotFound();
        return Ok(ticket);
    }

    [HttpPost("{id}/priority")]
    public async Task<IActionResult> SetPriority(string id, [FromBody] SetPriorityRequest request)
    {
        var ticket = await context.ServiceTickets.FirstOrDefaultAsync(t => t.Id == id);
        if (ticket == null) return NotFound();
        ticket.Priority = request.Priority;
        ticket.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(ticket);
    }
}

public record CreateTicketRequest(
    string ConversationId,
    string? Subject,
    TicketPriority? Priority,
    List<string>? Tags,
    bool AutoAssign = true);

public record AssignTicketRequest(string AssignedToUserId, string? AssignedToUserName);
public record SetPriorityRequest(TicketPriority Priority);
