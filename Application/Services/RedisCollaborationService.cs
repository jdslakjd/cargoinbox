using System.Text.Json;
using CargoInbox.Application.Services;
using StackExchange.Redis;

namespace CargoInbox.Application.Services;

public class RedisCollaborationService(IConnectionMultiplexer redis, ITenantProvider tenantProvider)
{
    private readonly IDatabase _db = redis.GetDatabase();

    private string GetTenantLockKey(string conversationId) => $"lock:{tenantProvider.TenantId}:{conversationId}";

    public async Task<bool> TryAcquireCollisionLockAsync(string conversationId, string userId, string userName)
    {
        var key = GetTenantLockKey(conversationId);
        var value = JsonSerializer.Serialize(new { UserId = userId, UserName = userName, LockedAt = DateTime.UtcNow });
        return await _db.StringSetAsync(key, value, TimeSpan.FromSeconds(15), When.NotExists);
    }

    public async Task ReleaseCollisionLockAsync(string conversationId, string userId)
    {
        var key = GetTenantLockKey(conversationId);
        var existing = await _db.StringGetAsync(key);
        if (!existing.IsNullOrEmpty)
        {
            var doc = JsonDocument.Parse(existing.ToString());
            if (doc.RootElement.GetProperty("UserId").GetString() == userId)
                await _db.KeyDeleteAsync(key);
        }
    }

    public async Task<object?> GetCollisionStatusAsync(string conversationId)
    {
        var key = GetTenantLockKey(conversationId);
        var existing = await _db.StringGetAsync(key);
        return existing.IsNullOrEmpty ? null : JsonSerializer.Deserialize<object>(existing.ToString());
    }
}
