namespace CargoInbox.Application.Services;

public class SequenceStepWorker(IServiceScopeFactory scopeFactory, ILogger<SequenceStepWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var engine = scope.ServiceProvider.GetRequiredService<SequenceEngineService>();
                await engine.ProcessPendingStepsAsync();
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sequence step worker failed");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
