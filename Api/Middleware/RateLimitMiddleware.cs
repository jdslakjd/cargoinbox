using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Api.Middleware;

public class RateLimitMiddleware
{
    private readonly RequestDelegate _next;
    private static readonly Dictionary<string, (int count, DateTime reset)> _counters = new();
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger<RateLimitMiddleware> _logger;

    public RateLimitMiddleware(RequestDelegate next, ILogger<RateLimitMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var key = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        var maxRequests = context.Request.Path.StartsWithSegments("/api/auth") ? 20 : 100;

        await _lock.WaitAsync();
        try
        {
            if (!_counters.TryGetValue(key, out var counter) || counter.reset < DateTime.UtcNow)
            {
                _counters[key] = (1, DateTime.UtcNow.AddMinutes(1));
            }
            else
            {
                if (counter.count >= maxRequests)
                {
                    context.Response.StatusCode = 429;
                    context.Response.Headers["Retry-After"] = ((int)(counter.reset - DateTime.UtcNow).TotalSeconds).ToString();
                    return;
                }
                _counters[key] = (counter.count + 1, counter.reset);
            }
        }
        finally { _lock.Release(); }

        await _next(context);
    }
}
