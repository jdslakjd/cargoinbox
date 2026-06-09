using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CargoInbox.Core.Configurations;
using Microsoft.Extensions.Options;

namespace CargoInbox.Application.Services;

public class AiTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly AiSettings _aiSettings;

    public AiTranslationService(HttpClient httpClient, IOptions<AiSettings> aiSettings)
    {
        _httpClient = httpClient;
        _aiSettings = aiSettings.Value;
    }

    public async Task<string> TranslateAsync(string text, string targetLanguage = "zh")
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;

        var payload = new
        {
            model = _aiSettings.ModelName,
            messages = new[]
            {
                new { role = "system", content = $"你是一个顶级跨境商务翻译专家。请将用户输入的文本翻译为指定的目标语言（代码：{targetLanguage}）。要求口吻专业、地道，直接输出译文，禁止包含任何解释或客套话。" },
                new { role = "user", content = text }
            },
            temperature = 0.3
        };

        return await CallDeepSeekAsync(payload);
    }

    public async Task<string> GenerateConversationSummaryAsync(string conversationTimelineText)
    {
        if (string.IsNullOrWhiteSpace(conversationTimelineText)) return "暂无上下文";

        var payload = new
        {
            model = _aiSettings.ModelName,
            messages = new[]
            {
                new { role = "system", content = "你是一个智能邮件分析师。请为下面一段客户沟通历史生成一句极简摘要。指出客户核心痛点和跟进状态。字数严控在 50 字以内，语气干练。" },
                new { role = "user", content = conversationTimelineText }
            },
            temperature = 0.5
        };

        return await CallDeepSeekAsync(payload);
    }

    public async Task<string> DetectLanguageAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";

        var payload = new
        {
            model = _aiSettings.ModelName,
            messages = new[]
            {
                new { role = "system", content = "你是一个语言检测器。只输出 ISO 639-1 语言代码（如 en, zh, es, ar, ja, fr），不要任何其他内容。" },
                new { role = "user", content = text }
            },
            temperature = 0
        };

        var result = await CallDeepSeekAsync(payload);
        return result.Trim().ToLower() switch
        {
            string s when s.Contains("en") => "en",
            string s when s.Contains("zh") => "zh",
            string s when s.Contains("es") => "es",
            string s when s.Contains("ar") => "ar",
            string s when s.Contains("ja") => "ja",
            string s when s.Contains("fr") => "fr",
            _ => result.Trim()
        };
    }

    private async Task<string> CallDeepSeekAsync(object payload)
    {
        var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_aiSettings.BaseUrl}/chat/completions");
        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _aiSettings.ApiKey);
        requestMessage.Content = JsonContent.Create(payload);

        try
        {
            var response = await _httpClient.SendAsync(requestMessage);
            if (!response.IsSuccessStatusCode)
            {
                var errContent = await response.Content.ReadAsStringAsync();
                return $"[AI Error] {response.StatusCode}: {errContent}";
            }

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync());
            return doc.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString()?.Trim() ?? "";
        }
        catch (Exception ex)
        {
            return $"[AI Exception] {ex.Message}";
        }
    }
}
