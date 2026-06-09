namespace CargoInbox.Application.Services;

public class TimezoneService(IHttpContextAccessor httpContextAccessor)
{
    private const string DefaultTimezone = "Asia/Shanghai";

    public string GetUserTimezone()
    {
        var claim = httpContextAccessor.HttpContext?.User?.FindFirst("timezone");
        return claim?.Value ?? DefaultTimezone;
    }

    public DateTime ToLocal(DateTime utc)
    {
        if (utc.Kind == DateTimeKind.Local) return utc;
        var tz = GetTimeZoneInfo();
        return TimeZoneInfo.ConvertTimeFromUtc(utc, tz);
    }

    public DateTime? ToLocal(DateTime? utc)
    {
        if (utc == null) return null;
        var tz = GetTimeZoneInfo();
        return TimeZoneInfo.ConvertTimeFromUtc(utc.Value, tz);
    }

    public DateTime ToUtc(DateTime local)
    {
        if (local.Kind == DateTimeKind.Utc) return local;
        var tz = GetTimeZoneInfo();
        return TimeZoneInfo.ConvertTimeToUtc(local, tz);
    }

    public string Format(DateTime utc, string format = "yyyy-MM-dd HH:mm")
    {
        return ToLocal(utc).ToString(format);
    }

    public string FormatRelative(DateTime utc)
    {
        var local = ToLocal(utc);
        var now = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, GetTimeZoneInfo());
        var diff = now - local;

        return diff.TotalMinutes switch
        {
            < 1 => "刚刚",
            < 60 => $"{(int)diff.TotalMinutes} 分钟前",
            < 1440 => $"{(int)diff.TotalHours} 小时前",
            < 43200 => $"{(int)diff.TotalDays} 天前",
            _ => local.ToString("yyyy-MM-dd")
        };
    }

    private TimeZoneInfo GetTimeZoneInfo()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(GetUserTimezone());
        }
        catch
        {
            return TimeZoneInfo.FindSystemTimeZoneById(DefaultTimezone);
        }
    }
}
