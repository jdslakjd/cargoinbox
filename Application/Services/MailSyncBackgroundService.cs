using CargoInbox.Core.Entities;
using CargoInbox.Core.Interfaces;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Application.Services;

public class MailSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IMailSyncService mailSyncService,
    ILogger<MailSyncBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CargoInbox 邮件增量同步后台搬运工已启动...");

        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));

        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

                var activeConfigs = await context.UserMailConfigs
                    .IgnoreQueryFilters()
                    .Where(c => c.ProviderType == MailProviderType.Custom_IMAP_SMTP)
                    .Select(c => c.Id)
                    .ToListAsync(stoppingToken);

                logger.LogInformation("检测到系统当前共有 {Count} 个外贸邮箱配置待同步...", activeConfigs.Count);

                foreach (var configId in activeConfigs)
                {
                    if (stoppingToken.IsCancellationRequested) break;

                    try
                    {
                        await mailSyncService.SyncUserMailsAsync(configId);
                    }
                    catch (Exception ex)
                    {
                        logger.LogError(ex, "同步邮箱配置 {ConfigId} 时发生致命异常", configId);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "邮件搬运工守护进程遭遇异常，等待下一轮调度...");
            }
        }
    }
}
