using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CargoInbox.Application.Services;

public class SequenceEngineService(IServiceScopeFactory scopeFactory)
{
    public async Task StartSequenceAsync(string sequenceId, string conversationId, string userId)
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var sequence = await context.Set<EmailSequence>()
            .Include(s => s.Steps.OrderBy(st => st.StepOrder))
            .FirstOrDefaultAsync(s => s.Id == sequenceId);

        if (sequence == null || sequence.Steps.Count == 0) return;

        var execution = new SequenceExecution
        {
            SequenceId = sequenceId,
            ConversationId = conversationId,
            UserId = userId,
            CurrentStep = 0,
            NextStepAt = DateTime.UtcNow.AddMinutes(sequence.DelayMinutes)
        };
        context.Set<SequenceExecution>().Add(execution);
        await context.SaveChangesAsync();
    }

    public async Task ProcessPendingStepsAsync()
    {
        using var scope = scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
        var mailSendService = scope.ServiceProvider.GetRequiredService<MailSendService>();

        var pendingExecutions = await context.Set<SequenceExecution>()
            .Where(e => !e.IsCompleted && e.NextStepAt <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var exec in pendingExecutions)
        {
            var sequence = await context.Set<EmailSequence>()
                .Include(s => s.Steps.OrderBy(st => st.StepOrder))
                .FirstOrDefaultAsync(s => s.Id == exec.SequenceId);

            if (sequence == null || exec.CurrentStep >= sequence.Steps.Count)
            {
                exec.IsCompleted = true;
                exec.NextStepAt = null;
                await context.SaveChangesAsync();
                continue;
            }

            var step = sequence.Steps[exec.CurrentStep];

            try
            {
                var conv = await context.Conversations
                    .Include(c => c.Contact)
                    .FirstOrDefaultAsync(c => c.Id == exec.ConversationId);

                await mailSendService.SendFromConversationAsync(
                    exec.ConversationId, exec.UserId, step.Subject, step.HtmlBody, step.TextBody, null,
                    new MailSendOptions
                    {
                        ToAddress = conv?.Contact?.Email,
                        BypassApproval = true
                    });
            }
            catch (Exception ex)
            {
                context.ActivityLogs.Add(new ActivityLog
                {
                    ConversationId = exec.ConversationId,
                    UserId = exec.UserId,
                    UserName = "SequenceEngine",
                    Action = "SequenceStepFailed",
                    Detail = $"Step {exec.CurrentStep + 1} failed: {ex.Message}"
                });
            }

            exec.CurrentStep++;
            if (exec.CurrentStep >= sequence.Steps.Count)
            {
                exec.IsCompleted = true;
                exec.NextStepAt = null;
            }
            else
            {
                exec.NextStepAt = DateTime.UtcNow.AddMinutes(step.DelayAfterPreviousMinutes);
            }
            await context.SaveChangesAsync();
        }
    }
}
