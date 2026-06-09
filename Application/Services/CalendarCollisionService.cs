using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class CalendarCollisionService(CargoInboxContext context)
{
    public async Task<bool> IsTimeSlotCollidedAsync(string userId, DateTime startUtc, DateTime endUtc, string? excludeEventId = null)
    {
        return await context.Set<CalendarEvent>()
            .AnyAsync(e => e.OrganizerUserId == userId
                        && !e.IsCancelled
                        && (excludeEventId == null || e.Id != excludeEventId)
                        && !(endUtc <= e.StartTimeUtc || startUtc >= e.EndTimeUtc));
    }
}
