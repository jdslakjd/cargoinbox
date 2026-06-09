namespace CargoInbox.Core.Entities;

public enum MeetingProvider
{
    Internal_LiveRoom,
    Google_Meet,
    Zoom,
    Teams
}

public class CalendarEvent
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string TenantId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }

    public DateTime StartTimeUtc { get; set; }
    public DateTime EndTimeUtc { get; set; }

    public string OrganizerUserId { get; set; } = string.Empty;
    public string OrganizerUserName { get; set; } = string.Empty;

    public string? RelatedContactId { get; set; }

    public MeetingProvider Provider { get; set; } = MeetingProvider.Internal_LiveRoom;
    public string? MeetingUrl { get; set; }
    public bool IsCancelled { get; set; }
}
