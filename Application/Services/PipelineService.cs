using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class PipelineService(CargoInboxContext context)
{
    private static readonly (string Name, int Order, int Probability)[] DefaultStages =
    [
        ("Qualified", 0, 20),
        ("Proposal", 1, 50),
        ("Negotiation", 2, 75),
        ("Closed Won", 3, 100),
        ("Closed Lost", 4, 0)
    ];

    public async Task<Pipeline> EnsureDefaultPipelineAsync(string tenantId, CancellationToken ct = default)
    {
        var pipeline = await context.Pipelines
            .Include(p => p.Stages)
            .FirstOrDefaultAsync(p => p.TenantId == tenantId && p.IsDefault, ct);

        if (pipeline != null)
        {
            pipeline.Stages = pipeline.Stages.OrderBy(s => s.SortOrder).ToList();
            return pipeline;
        }

        pipeline = new Pipeline
        {
            TenantId = tenantId,
            Name = "Sales Pipeline",
            IsDefault = true
        };

        foreach (var (name, order, probability) in DefaultStages)
        {
            pipeline.Stages.Add(new PipelineStage
            {
                TenantId = tenantId,
                Name = name,
                SortOrder = order,
                WinProbability = probability
            });
        }

        context.Pipelines.Add(pipeline);
        await context.SaveChangesAsync(ct);
        return pipeline;
    }
}
