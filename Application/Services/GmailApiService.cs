using System.Net.Http.Json;
using System.Text.Json.Serialization;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class GmailApiService(
    IServiceScopeFactory scopeFactory,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration,
    ILogger<GmailApiService> logger)
{
    public async Task<GmailSession?> GetSessionAsync(string userId, string email)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
        var token = await db.Set<OAuthToken>()
            .IgnoreQueryFilters()
            .Where(t => t.UserId == userId && t.Email == email && t.Provider == "Google")
            .OrderByDescending(t => t.CreatedAt)
            .FirstOrDefaultAsync();

        if (token == null || string.IsNullOrEmpty(token.AccessToken))
            return null;

        if (token.ExpiresAt <= DateTime.UtcNow.AddMinutes(5))
        {
            var refreshed = await RefreshTokenAsync(token);
            if (!refreshed) return null;
        }

        return new GmailSession { AccessToken = token.AccessToken, TokenId = token.Id };
    }

    public async Task<List<GmailMessageSummary>> ListMessagesAsync(string accessToken, string query, int maxResults = 50)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var url = $"https://gmail.googleapis.com/gmail/v1/users/me/messages?maxResults={maxResults}";
        if (!string.IsNullOrEmpty(query))
            url += "&q=" + Uri.EscapeDataString(query);

        var response = await client.GetAsync(url);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GmailMessageListResponse>();
        return result?.Messages ?? [];
    }

    public async Task<string> GetMessageRawAsync(string accessToken, string messageId)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync(
            $"https://gmail.googleapis.com/gmail/v1/users/me/messages/{messageId}?format=raw");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GmailRawMessageResponse>();
        if (result?.Raw == null)
            throw new InvalidOperationException("Empty raw message from Gmail API");

        var bytes = Convert.FromBase64String(result.Raw.Replace('-', '+').Replace('_', '/'));
        return System.Text.Encoding.UTF8.GetString(bytes);
    }

    public async Task<ulong> GetProfileHistoryIdAsync(string accessToken)
    {
        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

        var response = await client.GetAsync("https://gmail.googleapis.com/gmail/v1/users/me/profile");
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<GmailProfileResponse>();
        return result?.HistoryId ?? 0;
    }

    private async Task<bool> RefreshTokenAsync(OAuthToken token)
    {
        try
        {
            var opts = configuration.GetSection("GoogleOAuth");
            var clientId = opts["ClientId"];
            var clientSecret = opts["ClientSecret"];

            if (string.IsNullOrEmpty(token.RefreshToken))
            {
                logger.LogWarning("OAuthToken {TokenId} has no refresh token, cannot refresh", token.Id);
                return false;
            }

            var client = httpClientFactory.CreateClient();
            var response = await client.PostAsync("https://oauth2.googleapis.com/token",
                new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId ?? "",
                    ["client_secret"] = clientSecret ?? "",
                    ["refresh_token"] = token.RefreshToken,
                    ["grant_type"] = "refresh_token"
                }));

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<GoogleRefreshResponse>();
            if (result?.AccessToken == null) return false;

            token.AccessToken = result.AccessToken;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(result.ExpiresIn);

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
            db.Set<OAuthToken>().Update(token);
            await db.SaveChangesAsync();

            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to refresh OAuth token for {Email}", token.Email);
            return false;
        }
    }

    private class GoogleRefreshResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class GmailMessageListResponse
    {
        [JsonPropertyName("messages")] public List<GmailMessageSummary>? Messages { get; set; }
    }

    private class GmailRawMessageResponse
    {
        [JsonPropertyName("raw")] public string? Raw { get; set; }
    }

    private class GmailProfileResponse
    {
        [JsonPropertyName("historyId")] public ulong HistoryId { get; set; }
    }
}

public class GmailSession
{
    public string AccessToken { get; set; } = "";
    public string TokenId { get; set; } = "";
}

public class GmailMessageSummary
{
    [JsonPropertyName("id")] public string Id { get; set; } = "";
    [JsonPropertyName("threadId")] public string ThreadId { get; set; } = "";
}
