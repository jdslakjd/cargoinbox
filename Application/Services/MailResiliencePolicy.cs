using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;
using Polly.Timeout;

namespace CargoInbox.Application.Services;

public class MailResiliencePolicy
{
    private readonly ResiliencePipeline _pipeline;

    public MailResiliencePolicy(ILogger<MailResiliencePolicy> logger)
    {
        _pipeline = new ResiliencePipelineBuilder()
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30)
            })
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay = TimeSpan.FromSeconds(2),
                UseJitter = true,
                OnRetry = args =>
                {
                    logger.LogWarning("IMAP 操作失败，第 {Attempt} 次重试", args.AttemptNumber);
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }

    public ResiliencePipeline Pipeline => _pipeline;
}
