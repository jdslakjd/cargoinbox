using System.Collections.Concurrent;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace CargoInbox.Api.Hubs;

[Authorize]
public class CollaborationHub : Hub
{
    private static readonly ConcurrentDictionary<string, HashSet<string>> ConnectionGroups = new();

    private string CurrentTenantId => Context.User?.FindFirstValue("tenant_id") ?? "default";
    private string CurrentUserId => Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
    private string CurrentUserName => Context.User?.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";

    public override async Task OnConnectedAsync()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, CurrentTenantId);
        ConnectionGroups.TryAdd(Context.ConnectionId, [CurrentTenantId]);
        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        if (ConnectionGroups.TryRemove(Context.ConnectionId, out var groups))
        {
            foreach (var group in groups)
            {
                await Clients.OthersInGroup(group).SendAsync("OnUserTypingStatusChanged", new
                {
                    conversationId = "",
                    userId = CurrentUserId,
                    userName = CurrentUserName,
                    isTyping = false,
                    timestamp = DateTime.UtcNow
                });
            }
        }
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, CurrentTenantId);
        await base.OnDisconnectedAsync(exception);
    }

    public async Task JoinConversationGroup(string conversationId)
    {
        var group = $"conversation_{conversationId}";
        await Groups.AddToGroupAsync(Context.ConnectionId, group);
        ConnectionGroups.AddOrUpdate(Context.ConnectionId, _ => [group], (_, set) => { set.Add(group); return set; });
    }

    public async Task LeaveConversationGroup(string conversationId)
    {
        var group = $"conversation_{conversationId}";
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, group);
        if (ConnectionGroups.TryGetValue(Context.ConnectionId, out var set))
            set.Remove(group);
    }

    public async Task BroadcastTypingStatus(string conversationId, bool isTyping)
    {
        var group = $"conversation_{conversationId}";
        await Clients.OthersInGroup(group).SendAsync("OnUserTypingStatusChanged", new
        {
            conversationId,
            userId = CurrentUserId,
            userName = CurrentUserName,
            isTyping,
            timestamp = DateTime.UtcNow
        });
    }
}
