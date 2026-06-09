using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public record CrmTimelineEntry(
    string Id,
    string Type,
    string Title,
    string? Body,
    DateTime OccurredAt,
    string? UserName,
    string? RelatedEntityId);

public class CrmTimelineService(CargoInboxContext context)
{
    public async Task<List<CrmTimelineEntry>> BuildContactTimelineAsync(string contactId, int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var entries = new List<CrmTimelineEntry>();

        var activities = await context.CrmActivities
            .AsNoTracking()
            .Where(a => a.ContactId == contactId)
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        entries.AddRange(activities.Select(a => new CrmTimelineEntry(
            a.Id,
            a.Type.ToString(),
            a.Title,
            a.Body,
            a.CreatedAt,
            a.UserName,
            a.RelatedEntityId)));

        var conversations = await context.Conversations
            .AsNoTracking()
            .Where(c => c.ContactId == contactId)
            .OrderByDescending(c => c.CreatedAt)
            .Take(limit)
            .Select(c => new { c.Id, c.Title, c.Channel, c.CreatedAt, c.AssignedToUserName })
            .ToListAsync(ct);

        entries.AddRange(conversations.Select(c => new CrmTimelineEntry(
            $"conv-{c.Id}",
            nameof(CrmActivityType.Conversation),
            c.Title,
            c.Channel.ToString(),
            c.CreatedAt,
            c.AssignedToUserName,
            c.Id)));

        var meetings = await context.CalendarEvents
            .AsNoTracking()
            .Where(e => e.RelatedContactId == contactId && !e.IsCancelled)
            .OrderByDescending(e => e.StartTimeUtc)
            .Take(limit)
            .Select(e => new { e.Id, e.Title, e.Description, e.StartTimeUtc, e.OrganizerUserName })
            .ToListAsync(ct);

        entries.AddRange(meetings.Select(m => new CrmTimelineEntry(
            $"meet-{m.Id}",
            nameof(CrmActivityType.Meeting),
            m.Title,
            m.Description,
            m.StartTimeUtc,
            m.OrganizerUserName,
            m.Id)));

        var calls = await context.CallLogs
            .AsNoTracking()
            .Where(c => c.ContactId == contactId)
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(limit)
            .Select(c => new { c.Id, c.PhoneNumber, c.DurationSeconds, c.AudioToTextTranscript, c.CreatedAtUtc })
            .ToListAsync(ct);

        entries.AddRange(calls.Select(c => new CrmTimelineEntry(
            $"call-{c.Id}",
            nameof(CrmActivityType.Call),
            $"Call · {c.PhoneNumber}",
            string.IsNullOrWhiteSpace(c.AudioToTextTranscript)
                ? $"{c.DurationSeconds}s"
                : c.AudioToTextTranscript,
            c.CreatedAtUtc,
            null,
            c.Id)));

        return entries
            .OrderByDescending(e => e.OccurredAt)
            .Take(limit)
            .ToList();
    }

    public async Task<List<CrmTimelineEntry>> BuildCompanyTimelineAsync(string companyId, int limit = 50, CancellationToken ct = default)
    {
        limit = Math.Clamp(limit, 1, 100);
        var contactIds = await context.Contacts
            .AsNoTracking()
            .Where(c => c.CompanyId == companyId)
            .Select(c => c.Id)
            .ToListAsync(ct);

        var entries = new List<CrmTimelineEntry>();

        var activities = await context.CrmActivities
            .AsNoTracking()
            .Where(a => a.CompanyId == companyId || (a.ContactId != null && contactIds.Contains(a.ContactId)))
            .OrderByDescending(a => a.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        entries.AddRange(activities.Select(a => new CrmTimelineEntry(
            a.Id,
            a.Type.ToString(),
            a.Title,
            a.Body,
            a.CreatedAt,
            a.UserName,
            a.RelatedEntityId)));

        return entries.OrderByDescending(e => e.OccurredAt).Take(limit).ToList();
    }
}
