using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings/channels")]
public class ChannelSettingsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    ChannelOutboundService channelOutboundService) : ControllerBase
{
    public record ChannelSettingsResponse(
        string? WhatsAppPhoneNumberId,
        string? WhatsAppAccessTokenMasked,
        string? FacebookPageAccessTokenMasked,
        string? FacebookPageId,
        bool WhatsAppConfigured,
        bool FacebookConfigured);

    public record ChannelSettingsUpdateRequest(
        string? WhatsAppPhoneNumberId,
        string? WhatsAppAccessToken,
        string? FacebookPageAccessToken,
        string? FacebookPageId);

    public record TestWhatsAppRequest(string PhoneNumber, string? Message);

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        var cfg = await context.TenantChannelConfigs
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantProvider.TenantId);

        return Ok(new ChannelSettingsResponse(
            cfg?.WhatsAppPhoneNumberId,
            MaskToken(cfg?.WhatsAppAccessToken),
            MaskToken(cfg?.FacebookPageAccessToken),
            cfg?.FacebookPageId,
            !string.IsNullOrWhiteSpace(cfg?.WhatsAppAccessToken) && !string.IsNullOrWhiteSpace(cfg?.WhatsAppPhoneNumberId),
            !string.IsNullOrWhiteSpace(cfg?.FacebookPageAccessToken)));
    }

    [HttpPut]
    public async Task<IActionResult> Update([FromBody] ChannelSettingsUpdateRequest request)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        var cfg = await context.TenantChannelConfigs
            .FirstOrDefaultAsync(c => c.TenantId == tenantProvider.TenantId);

        if (cfg == null)
        {
            cfg = new TenantChannelConfig { TenantId = tenantProvider.TenantId };
            context.TenantChannelConfigs.Add(cfg);
        }

        if (request.WhatsAppPhoneNumberId != null)
            cfg.WhatsAppPhoneNumberId = string.IsNullOrWhiteSpace(request.WhatsAppPhoneNumberId) ? null : request.WhatsAppPhoneNumberId.Trim();

        if (!string.IsNullOrWhiteSpace(request.WhatsAppAccessToken) && !IsMasked(request.WhatsAppAccessToken))
            cfg.WhatsAppAccessToken = request.WhatsAppAccessToken.Trim();

        if (request.FacebookPageId != null)
            cfg.FacebookPageId = string.IsNullOrWhiteSpace(request.FacebookPageId) ? null : request.FacebookPageId.Trim();

        if (!string.IsNullOrWhiteSpace(request.FacebookPageAccessToken) && !IsMasked(request.FacebookPageAccessToken))
            cfg.FacebookPageAccessToken = request.FacebookPageAccessToken.Trim();

        cfg.UpdatedAt = DateTime.UtcNow;
        await context.SaveChangesAsync();

        return Ok(new ChannelSettingsResponse(
            cfg.WhatsAppPhoneNumberId,
            MaskToken(cfg.WhatsAppAccessToken),
            MaskToken(cfg.FacebookPageAccessToken),
            cfg.FacebookPageId,
            !string.IsNullOrWhiteSpace(cfg.WhatsAppAccessToken) && !string.IsNullOrWhiteSpace(cfg.WhatsAppPhoneNumberId),
            !string.IsNullOrWhiteSpace(cfg.FacebookPageAccessToken)));
    }

    [HttpPost("test-whatsapp")]
    public async Task<IActionResult> TestWhatsApp([FromBody] TestWhatsAppRequest request)
    {
        if (!InboxPermissionService.IsAdmin(User)) return Forbid();

        if (string.IsNullOrWhiteSpace(request.PhoneNumber))
            return BadRequest(new { message = "Phone number is required" });

        var message = string.IsNullOrWhiteSpace(request.Message)
            ? "CargoInbox channel test message"
            : request.Message.Trim();

        var sent = await channelOutboundService.SendWhatsAppAsync(
            tenantProvider.TenantId, request.PhoneNumber.Trim(), message);

        return sent
            ? Ok(new { success = true, message = "Test message sent" })
            : StatusCode(502, new { message = "WhatsApp send failed. Check Phone Number ID and access token in channel settings." });
    }

    private static string? MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return null;
        if (token.Length <= 8) return "••••••••";
        return $"••••{token[^4..]}";
    }

    private static bool IsMasked(string value) => value.Contains('•');
}
