using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json.Serialization;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CargoInbox.Api.Controllers;

public class GoogleOAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}

public class MicrosoftOAuthOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public string RedirectUri { get; set; } = "";
}

[Authorize]
[ApiController]
[Route("api/oauth")]
public class OAuthController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    IOptions<GoogleOAuthOptions> googleOptions,
    IOptions<MicrosoftOAuthOptions> microsoftOptions,
    IHttpClientFactory httpClientFactory) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    public record OAuthCallbackRequest(string Provider, string Code, string State, string? RedirectUri);

    [HttpGet("config")]
    public IActionResult GetOAuthConfig()
    {
        var google = googleOptions.Value;
        var microsoft = microsoftOptions.Value;
        var defaultRedirect = $"{Request.Scheme}://{Request.Host}/auth/callback";

        return Ok(new
        {
            clientId = google.ClientId,
            redirectUri = string.IsNullOrWhiteSpace(google.RedirectUri) ? defaultRedirect : google.RedirectUri,
            google = new
            {
                clientId = google.ClientId,
                redirectUri = string.IsNullOrWhiteSpace(google.RedirectUri) ? defaultRedirect : google.RedirectUri
            },
            microsoft = new
            {
                clientId = microsoft.ClientId,
                redirectUri = string.IsNullOrWhiteSpace(microsoft.RedirectUri) ? defaultRedirect : microsoft.RedirectUri
            }
        });
    }

    [HttpPost("callback")]
    public async Task<IActionResult> OAuthCallback([FromBody] OAuthCallbackRequest request)
    {
        try
        {
            if (request.Provider is not ("Google" or "Outlook"))
                return BadRequest(new { error = "不支持的 OAuth Provider" });

            if (request.Provider == "Google")
                return await HandleGoogleCallbackAsync(request);

            return await HandleOutlookCallbackAsync(request);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "OAuth callback failed", details = ex.ToString() });
        }
    }

    private async Task<IActionResult> HandleGoogleCallbackAsync(OAuthCallbackRequest request)
    {
        var opts = googleOptions.Value;
        var redirectUri = ResolveRedirectUri(request.RedirectUri, opts.RedirectUri);

        var client = httpClientFactory.CreateClient();
        var tokenResponse = await client.PostAsync("https://oauth2.googleapis.com/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = request.Code,
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code"
            }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync();
            return BadRequest(new { error = "Token exchange failed", details = errorBody });
        }

        var result = await tokenResponse.Content.ReadFromJsonAsync<GoogleTokenResponse>();
        if (result == null)
            return BadRequest(new { error = "Empty token response" });

        var email = await ResolveGoogleEmailAsync(client, result);
        var mailConfig = await UpsertOAuthMailboxAsync(
            email, "Google", result.AccessToken, result.RefreshToken, result.ExpiresIn,
            MailProviderType.Gmail_OAuth2,
            "imap.gmail.com", 993, true,
            "smtp.gmail.com", 587, false,
            $"成功绑定 Gmail OAuth 邮箱: {email}");

        return Ok(new { success = true, provider = "Google", email, mailboxId = mailConfig.Id });
    }

    private async Task<IActionResult> HandleOutlookCallbackAsync(OAuthCallbackRequest request)
    {
        var opts = microsoftOptions.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId) || string.IsNullOrWhiteSpace(opts.ClientSecret))
            return BadRequest(new { error = "Microsoft OAuth is not configured on the server" });

        var redirectUri = ResolveRedirectUri(request.RedirectUri, opts.RedirectUri);
        var client = httpClientFactory.CreateClient();

        var tokenResponse = await client.PostAsync(
            "https://login.microsoftonline.com/common/oauth2/v2.0/token",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = request.Code,
                ["client_id"] = opts.ClientId,
                ["client_secret"] = opts.ClientSecret,
                ["redirect_uri"] = redirectUri,
                ["grant_type"] = "authorization_code",
                ["scope"] = "https://outlook.office.com/IMAP.AccessAsUser.All https://outlook.office.com/SMTP.Send offline_access openid email"
            }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var errorBody = await tokenResponse.Content.ReadAsStringAsync();
            return BadRequest(new { error = "Token exchange failed", details = errorBody });
        }

        var result = await tokenResponse.Content.ReadFromJsonAsync<MicrosoftTokenResponse>();
        if (result == null)
            return BadRequest(new { error = "Empty token response" });

        var email = await ResolveMicrosoftEmailAsync(client, result.AccessToken);
        var mailConfig = await UpsertOAuthMailboxAsync(
            email, "Outlook", result.AccessToken, result.RefreshToken, result.ExpiresIn,
            MailProviderType.Outlook_Office365,
            "outlook.office365.com", 993, true,
            "smtp.office365.com", 587, false,
            $"成功绑定 Outlook OAuth 邮箱: {email}");

        return Ok(new { success = true, provider = "Outlook", email, mailboxId = mailConfig.Id });
    }

    private string ResolveRedirectUri(string? requestRedirectUri, string? configuredRedirectUri)
    {
        if (!string.IsNullOrWhiteSpace(requestRedirectUri))
            return requestRedirectUri;
        if (!string.IsNullOrWhiteSpace(configuredRedirectUri))
            return configuredRedirectUri;
        return $"{Request.Scheme}://{Request.Host}/auth/callback";
    }

    private async Task<string> ResolveGoogleEmailAsync(HttpClient client, GoogleTokenResponse result)
    {
        var email = result.Email;
        if (string.IsNullOrEmpty(email) && !string.IsNullOrEmpty(result.IdToken))
        {
            try
            {
                var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                var jwt = handler.ReadJwtToken(result.IdToken);
                email = jwt.Claims.FirstOrDefault(c => c.Type == "email")?.Value;
            }
            catch { }
        }

        if (string.IsNullOrEmpty(email))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", result.AccessToken);
            try
            {
                var userInfo = await client.GetFromJsonAsync<GoogleUserInfoResponse>(
                    "https://www.googleapis.com/oauth2/v2/userinfo");
                email = userInfo?.Email;
            }
            catch { }
        }

        return email ?? "unknown@cargoinbox.cn";
    }

    private async Task<string> ResolveMicrosoftEmailAsync(HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        try
        {
            var profile = await client.GetFromJsonAsync<MicrosoftUserResponse>("https://graph.microsoft.com/v1.0/me");
            if (!string.IsNullOrWhiteSpace(profile?.Mail))
                return profile.Mail.Trim();
            if (!string.IsNullOrWhiteSpace(profile?.UserPrincipalName))
                return profile.UserPrincipalName.Trim();
        }
        catch { }

        return "unknown@cargoinbox.cn";
    }

    private async Task<UserMailConfig> UpsertOAuthMailboxAsync(
        string email,
        string provider,
        string accessToken,
        string? refreshToken,
        int expiresIn,
        MailProviderType providerType,
        string imapHost,
        int imapPort,
        bool imapUseSsl,
        string smtpHost,
        int smtpPort,
        bool smtpUseSsl,
        string activityDetail)
    {
        var existingToken = await context.OAuthTokens
            .FirstOrDefaultAsync(t => t.UserId == CurrentUserId && t.Email == email && t.Provider == provider);

        if (existingToken != null)
        {
            existingToken.AccessToken = accessToken;
            if (!string.IsNullOrEmpty(refreshToken))
                existingToken.RefreshToken = refreshToken;
            existingToken.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        }
        else
        {
            context.OAuthTokens.Add(new OAuthToken
            {
                TenantId = tenantProvider.TenantId,
                UserId = CurrentUserId,
                Provider = provider,
                Email = email,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
            });
        }

        var mailConfig = await context.UserMailConfigs
            .FirstOrDefaultAsync(c => c.UserId == CurrentUserId && c.EmailAddress == email);

        if (mailConfig == null)
        {
            mailConfig = new UserMailConfig
            {
                UserId = CurrentUserId,
                EmailAddress = email,
                ProviderType = providerType,
                ImapHost = imapHost,
                ImapPort = imapPort,
                ImapUseSsl = imapUseSsl,
                SmtpHost = smtpHost,
                SmtpPort = smtpPort,
                SmtpUseSsl = smtpUseSsl,
                TenantId = tenantProvider.TenantId
            };
            context.UserMailConfigs.Add(mailConfig);
        }
        else
        {
            mailConfig.ProviderType = providerType;
            mailConfig.ImapHost = imapHost;
            mailConfig.ImapPort = imapPort;
            mailConfig.ImapUseSsl = imapUseSsl;
            mailConfig.SmtpHost = smtpHost;
            mailConfig.SmtpPort = smtpPort;
            mailConfig.SmtpUseSsl = smtpUseSsl;
        }

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous",
            Action = "BindMailbox",
            Detail = activityDetail,
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return mailConfig;
    }

    private class GoogleTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
        [JsonPropertyName("email")] public string? Email { get; set; }
        [JsonPropertyName("id_token")] public string? IdToken { get; set; }
    }

    private class GoogleUserInfoResponse
    {
        [JsonPropertyName("email")] public string Email { get; set; } = "";
    }

    private class MicrosoftTokenResponse
    {
        [JsonPropertyName("access_token")] public string AccessToken { get; set; } = "";
        [JsonPropertyName("refresh_token")] public string? RefreshToken { get; set; }
        [JsonPropertyName("expires_in")] public int ExpiresIn { get; set; }
    }

    private class MicrosoftUserResponse
    {
        [JsonPropertyName("mail")] public string? Mail { get; set; }
        [JsonPropertyName("userPrincipalName")] public string? UserPrincipalName { get; set; }
    }

    [HttpGet("tokens")]
    public async Task<IActionResult> GetOAuthTokens([FromQuery] string? provider)
    {
        var query = context.Set<OAuthToken>().AsNoTracking().Where(t => t.UserId == CurrentUserId);
        if (!string.IsNullOrEmpty(provider)) query = query.Where(t => t.Provider == provider);

        return Ok(await query.OrderByDescending(t => t.CreatedAt).Take(20).ToListAsync());
    }
}
