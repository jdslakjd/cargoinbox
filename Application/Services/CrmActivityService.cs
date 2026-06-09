using System.Security.Claims;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;

namespace CargoInbox.Application.Services;

public class CrmActivityService(CargoInboxContext context, ITenantProvider tenantProvider)
{
    public async Task<CrmActivity> LogAsync(
        CrmActivityType type,
        string title,
        string? body = null,
        string? contactId = null,
        string? companyId = null,
        string? relatedEntityId = null,
        string? userId = null,
        string? userName = null,
        CancellationToken cancellationToken = default)
    {
        var activity = new CrmActivity
        {
            TenantId = tenantProvider.TenantId,
            ContactId = contactId,
            CompanyId = companyId,
            Type = type,
            Title = title,
            Body = body,
            RelatedEntityId = relatedEntityId,
            UserId = userId ?? "system",
            UserName = userName ?? "System"
        };

        context.CrmActivities.Add(activity);
        await context.SaveChangesAsync(cancellationToken);
        return activity;
    }

    public static (string? userId, string? userName) ResolveActor(ClaimsPrincipal user)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier);
        var userName = user.FindFirstValue(ClaimTypes.Name)
            ?? user.FindFirstValue(ClaimTypes.GivenName)
            ?? user.Identity?.Name;
        return (userId, userName);
    }
}
