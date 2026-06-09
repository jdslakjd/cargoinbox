using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class SnoozeAutoWakeWorker(IServiceScopeFactory scopeFactory, ILogger<SnoozeAutoWakeWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
                var now = DateTime.UtcNow;

                var due = await context.Conversations
                    .IgnoreQueryFilters()
                    .Where(c => c.Status == MailStatus.Snoozed && c.SnoozedUntil != null && c.SnoozedUntil <= now)
                    .ToListAsync(stoppingToken);

                foreach (var conv in due)
                {
                    conv.Status = MailStatus.Open;
                    conv.SnoozedUntil = null;
                }

                if (due.Count > 0)
                {
                    await context.SaveChangesAsync(stoppingToken);
                    logger.LogInformation("Auto-woke {Count} snoozed conversations", due.Count);
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Snooze auto-wake worker failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
