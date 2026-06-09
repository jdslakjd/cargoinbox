using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CargoInbox.Application.Services;

public class OutlookApiService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<OutlookApiService> logger)
{
    public async Task<OutlookSession?> GetSessionAsync(string userId, string email)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

        var token = await db.OAuthTokens
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.Email == email && t.Provider == "Outlook")
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (token == null) return null;

        if (token.ExpiresAt <= DateTime.UtcNow.AddMinutes(2))
        {
            var refreshed = await RefreshTokenAsync(token);
            if (!refreshed) return null;
        }

        return new OutlookSession { AccessToken = token.AccessToken, TokenId = token.Id };
    }

    private async Task<bool> RefreshTokenAsync(OAuthToken token)
    {
        try
        {
            var clientId = configuration["MicrosoftOAuth:ClientId"];
            var clientSecret = configuration["MicrosoftOAuth:ClientSecret"];
            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(clientSecret))
                return false;

            if (string.IsNullOrEmpty(token.RefreshToken))
            {
                logger.LogWarning("Outlook token {TokenId} has no refresh token", token.Id);
                return false;
            }

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

            var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync(
                "https://login.microsoftonline.com/common/oauth2/v2.0/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId,
                    ["client_secret"] = clientSecret,
                    ["refresh_token"] = token.RefreshToken,
                    ["grant_type"] = "refresh_token",
                    ["scope"] = "https://outlook.office.com/SMTP.Send offline_access"
                }));

            if (!response.IsSuccessStatusCode) return false;

            var json = await response.Content.ReadFromJsonAsync<MicrosoftTokenResponse>();
            if (json == null) return false;

            token.AccessToken = json.AccessToken;
            if (!string.IsNullOrEmpty(json.RefreshToken))
                token.RefreshToken = json.RefreshToken;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(json.ExpiresIn);

            db.OAuthTokens.Update(token);
            await db.SaveChangesAsync();
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh Outlook token for {Email}", token.Email);
            return false;
        }
    }

    private class MicrosoftTokenResponse
    {
        [System.Text.Json.Serialization.JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = "";
        [System.Text.Json.Serialization.JsonPropertyName("refresh_token")]
        public string? RefreshToken { get; set; }
        [System.Text.Json.Serialization.JsonPropertyName("expires_in")]
        public int ExpiresIn { get; set; }
    }
}

public class OutlookSession
{
    public string AccessToken { get; set; } = "";
    public string TokenId { get; set; } = "";
}
