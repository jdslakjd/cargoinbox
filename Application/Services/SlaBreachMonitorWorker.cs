using CargoInbox.Api.Hubs;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Application.Services;

public class SlaBreachMonitorWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaBreachMonitorWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CargoInbox SLA 服务等级协议效能流转引擎已挂载启动...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<CollaborationHub>>();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();

                var limitTime = DateTime.UtcNow.AddHours(-2);

                var breachedConversations = await db.Conversations
                    .IgnoreQueryFilters()
                    .Where(c => c.Status == MailStatus.Assigned
                        && c.AssignedAt <= limitTime
                        && !c.IsSlaBreached
                        && c.FirstRepliedAtUtc == null)
                    .ToListAsync(stoppingToken);

                if (breachedConversations.Any())
                {
                    foreach (var conv in breachedConversations)
                    {
                        conv.IsSlaBreached = true;
                        conv.Status = MailStatus.Open;
                        var oldAssignedUser = conv.AssignedToUserId;
                        conv.AssignedToUserId = null;

                        db.MailComments.Add(new MailComment
                        {
                            ConversationId = conv.Id,
                            UserId = "system-sla",
                            UserName = "SLA警报时钟",
                            Content = $"🚨【SLA 违约超时红牌】该会话分配给成员 [{oldAssignedUser}] 超过 2 小时无任何有效回复！系统已强行收回指派退回公海，并向管理层记下一次违约记录。"
                        });

                        await hubContext.Clients.All.SendAsync("OnSlaBreachAlert", new { conversationId = conv.Id, breachedUser = oldAssignedUser });

                        if (!string.IsNullOrEmpty(oldAssignedUser))
                            await notificationService.NotifySlaBreachAsync(oldAssignedUser, conv.Id);
                    }

                    await db.SaveChangesAsync(stoppingToken);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sla 扫描引擎发生物理报错");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
