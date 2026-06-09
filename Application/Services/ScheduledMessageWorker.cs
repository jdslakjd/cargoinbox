using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Application.Services;

public class ScheduledMessageWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<ScheduledMessageWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("定时发送调度器已启动");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                var mailService = scope.ServiceProvider.GetRequiredService<MailSendService>();

                var dueMessages = await context.Set<ScheduledMessage>()
                    .Where(m => !m.IsSent && m.ScheduledAtUtc <= DateTime.UtcNow)
                    .Take(20)
                    .ToListAsync(stoppingToken);

                foreach (var msg in dueMessages)
                {
                    try
                    {
                        await mailService.SendFromConversationAsync(msg.ConversationId, msg.UserId, msg.Subject, msg.HtmlBody, msg.TextBody, msg.CcAddress);
                        msg.IsSent = true;
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "定时发送失败 {Id}", msg.Id);
                    }
                }

                await context.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "定时发送调度器异常");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}
