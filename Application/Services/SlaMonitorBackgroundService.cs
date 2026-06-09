using CargoInbox.Api.Hubs;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Application.Services;

public class SlaMonitorBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<SlaMonitorBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CargoInbox SLA monitor started (policy-driven, 60s interval)");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var slaTracker = scope.ServiceProvider.GetRequiredService<SlaTrackerService>();
                var notificationService = scope.ServiceProvider.GetRequiredService<NotificationService>();
                var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<CollaborationHub>>();
                var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

                var newlyBreached = await slaTracker.CheckAndMarkSlaBreachesAsync();

                foreach (var conv in newlyBreached)
                {
                    var notifyUser = conv.AssignedToUserId ?? conv.UserId;
                    context.MailComments.Add(new MailComment
                    {
                        ConversationId = conv.Id,
                        UserId = "system-sla",
                        UserName = "SLA Monitor",
                        Content = "SLA policy breached — please respond or resolve this conversation promptly."
                    });

                    await hubContext.Clients.All.SendAsync(
                        "OnSlaBreachAlert",
                        new { conversationId = conv.Id, breachedUser = conv.AssignedToUserId },
                        stoppingToken);

                    if (!string.IsNullOrEmpty(notifyUser))
                        await notificationService.NotifySlaBreachAsync(notifyUser, conv.Id);
                }

                if (newlyBreached.Count > 0)
                    await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "SLA monitor error");
            }

            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }
    }
}
