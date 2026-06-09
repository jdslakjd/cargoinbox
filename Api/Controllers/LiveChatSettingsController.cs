using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings/livechat")]
public class LiveChatSettingsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    LiveChatService liveChatService,
    IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var widget = await liveChatService.EnsureDefaultWidgetAsync(tenantProvider.TenantId);
        var apiBase = GetApiBaseUrl();
        return Ok(new
        {
            widget.Id,
            widget.PublicKey,
            widget.Name,
            widget.WelcomeMessage,
            widget.OfflineMessage,
            widget.PrimaryColor,
            widget.Position,
            widget.IsEnabled,
            widget.UpdatedAt,
            apiBaseUrl = apiBase,
            embedScript = $"<script src=\"{apiBase}/widget/cargoinbox-widget.js\" data-widget-key=\"{widget.PublicKey}\" async></script>",
            previewUrl = $"{apiBase}/widget/demo.html?key={widget.PublicKey}"
        });
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] LiveChatWidgetUpdateRequest request)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var widget = await liveChatService.EnsureDefaultWidgetAsync(tenantProvider.TenantId);

        if (!string.IsNullOrWhiteSpace(request.Name)) widget.Name = request.Name.Trim();
        if (request.WelcomeMessage != null) widget.WelcomeMessage = request.WelcomeMessage;
        if (request.OfflineMessage != null) widget.OfflineMessage = request.OfflineMessage;
        if (!string.IsNullOrWhiteSpace(request.PrimaryColor)) widget.PrimaryColor = request.PrimaryColor.Trim();
        if (!string.IsNullOrWhiteSpace(request.Position)) widget.Position = request.Position.Trim();
        if (request.IsEnabled.HasValue) widget.IsEnabled = request.IsEnabled.Value;

        widget.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return await Get();
    }

    [HttpPost("regenerate-key")]
    public async Task<IActionResult> RegenerateKey()
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var widget = await liveChatService.EnsureDefaultWidgetAsync(tenantProvider.TenantId);
        widget.PublicKey = $"ci_{Guid.NewGuid():N}";
        widget.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return await Get();
    }

    private string GetApiBaseUrl()
    {
        var configured = configuration["LiveChat:PublicApiBaseUrl"];
        if (!string.IsNullOrWhiteSpace(configured))
            return configured.TrimEnd('/');

        var request = HttpContext.Request;
        return $"{request.Scheme}://{request.Host}";
    }
}

public record LiveChatWidgetUpdateRequest(
    string? Name,
    string? WelcomeMessage,
    string? OfflineMessage,
    string? PrimaryColor,
    string? Position,
    bool? IsEnabled);
