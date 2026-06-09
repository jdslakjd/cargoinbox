using StackExchange.Redis;

namespace CargoInbox.Api.Hubs;

public class TypingPresenceTracker(IConnectionMultiplexer redis)
{
    private readonly IDatabase _db = redis.GetDatabase();

    public async Task SetTypingAsync(string conversationId, string userId, string userName)
    {
        var key = $"cargoinbox:typing:{conversationId}";
        await _db.HashSetAsync(key, userId, userName);
        await _db.KeyExpireAsync(key, TimeSpan.FromSeconds(15));
    }

    public async Task RemoveTypingAsync(string conversationId, string userId)
    {
        var key = $"cargoinbox:typing:{conversationId}";
        await _db.HashDeleteAsync(key, userId);
    }

    public async Task RemoveConnectionAsync(string connectionId)
    {
        var keys = _db.Multiplexer.GetServer(_db.Multiplexer.GetEndPoints()[0]);
        // Clean up is handled by OnDisconnectedAsync via in-memory tracking
        await Task.CompletedTask;
    }
}
