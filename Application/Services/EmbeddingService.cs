using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;

namespace CargoInbox.Application.Services;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text);
    Task VectorizeAndSaveAsync(string messageId, string text);
}

public class EmbeddingService : IEmbeddingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _config;
    private readonly IServiceScopeFactory _scopeFactory;

    public EmbeddingService(IHttpClientFactory httpClientFactory, IConfiguration config, IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _config = config;
        _scopeFactory = scopeFactory;
    }

    public async Task<float[]> GetEmbeddingAsync(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return Array.Empty<float>();

        var client = _httpClientFactory.CreateClient();
        var apiKey = _config["AISettings:ApiKey"];
        var endpoint = _config["AISettings:Endpoint"]?.TrimEnd('/') + "/embeddings";
        var modelId = _config["AISettings:EmbeddingModelId"];

        var requestBody = new { model = modelId, input = text.Replace("\r", "").Replace("\n", " ") };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
        var jsonContent = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json");

        try
        {
            var response = await client.PostAsync(endpoint, jsonContent);
            var resultJson = await response.Content.ReadAsStringAsync();
            if (!response.IsSuccessStatusCode)
                throw new Exception($"向量接口返回错误: {response.StatusCode} - {resultJson}");

            using var doc = JsonDocument.Parse(resultJson);
            var embeddingElement = doc.RootElement.GetProperty("data")[0].GetProperty("embedding");
            return embeddingElement.EnumerateArray().Select(x => (float)x.GetDouble()).ToArray();
        }
        catch (Exception ex)
        {
            throw new Exception($"生成向量失败: {ex.Message}");
        }
    }

    public async Task VectorizeAndSaveAsync(string messageId, string text)
    {
        try
        {
            var vector = await GetEmbeddingAsync(text.Length > 1000 ? text[..1000] : text);
            if (vector.Length == 0) return;
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
            var message = await context.ConversationMessages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (message != null) { message.Embedding = new Vector(vector); await context.SaveChangesAsync(); }
        }
        catch { }
    }
}
