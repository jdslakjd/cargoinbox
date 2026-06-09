using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CargoInbox.Application.Services;

public class ChannelOutboundService(
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory)
{
    private async Task<TenantChannelConfig?> GetTenantConfigAsync(string tenantId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
        return await db.TenantChannelConfigs
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.TenantId == tenantId);
    }

    public async Task<bool> SendWhatsAppAsync(string tenantId, string phoneNumber, string message)
    {
        try
        {
            var cfg = await GetTenantConfigAsync(tenantId);
            var phoneNumberId = cfg?.WhatsAppPhoneNumberId ?? configuration["WhatsApp:PhoneNumberId"];
            var token = cfg?.WhatsAppAccessToken ?? configuration["WhatsApp:AccessToken"];
            if (string.IsNullOrWhiteSpace(phoneNumberId) || string.IsNullOrWhiteSpace(token))
                return false;

            var client = httpClientFactory.CreateClient();
            var url = $"https://graph.facebook.com/v18.0/{phoneNumberId}/messages";
            var payload = new
            {
                messaging_product = "whatsapp",
                to = phoneNumber,
                type = "text",
                text = new { body = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public async Task<bool> SendFacebookAsync(string tenantId, string psid, string message)
    {
        try
        {
            var cfg = await GetTenantConfigAsync(tenantId);
            var token = cfg?.FacebookPageAccessToken ?? configuration["Facebook:PageAccessToken"];
            if (string.IsNullOrWhiteSpace(token))
                return false;

            var client = httpClientFactory.CreateClient();
            var url = "https://graph.facebook.com/v18.0/me/messages";
            var payload = new
            {
                recipient = new { id = psid },
                message = new { text = message }
            };

            var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
            request.Content = JsonContent.Create(payload);

            var response = await client.SendAsync(request);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}
