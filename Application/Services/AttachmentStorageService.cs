using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;

namespace CargoInbox.Application.Services;

public class AttachmentStorageService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IServiceScopeFactory _scopeFactory;
    private const string ServerUrl = "https://amazons3fileupload-amazons3fileupload.apps.v8eq.hk.topkee.top";
    private const string ApiToken = "uEXvqg8nlvuvtc7Q4SBnYT";

    public AttachmentStorageService(IHttpClientFactory httpClientFactory, IServiceScopeFactory scopeFactory)
    {
        _httpClientFactory = httpClientFactory;
        _scopeFactory = scopeFactory;
    }

    public async Task<Attachment> StoreAsync(Stream stream, string fileName, string contentType, string messageId)
    {
        string extension = Path.GetExtension(fileName);
        string safeFileName = $"{Guid.NewGuid():N}{extension}";

        // Capture length before upload disposes the stream
        long sizeBytes = stream.Length;
        var uploadResult = await UploadToS3(safeFileName, stream, "annex", "public");
        var resultJson = JsonDocument.Parse(uploadResult).RootElement;

        if (resultJson.TryGetProperty("code", out var codeEl) &&
            codeEl.GetString()?.ToLower() == "ok" &&
            resultJson.TryGetProperty("filePath", out var filePathEl))
        {
            string filePath = filePathEl.GetString()!;
            string preSignedUrl = await GetFileUrl(filePath, true);
            string finalUrl = preSignedUrl.Contains('?') ? preSignedUrl.Split('?')[0] : preSignedUrl;

            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();

            var attachment = new Attachment
            {
                MessageId = messageId,
                FileName = fileName,
                ContentType = contentType,
                SizeBytes = sizeBytes,
                FileUrl = finalUrl,
                FilePath = filePath,
                StorageProvider = "S3"
            };

            context.Set<Attachment>().Add(attachment);
            await context.SaveChangesAsync();
            return attachment;
        }

        throw new InvalidOperationException($"S3 upload failed: {uploadResult}");
    }

    public async Task<Stream?> RetrieveAsync(string attachmentId)
    {
        using var scope = _scopeFactory.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<CargoInboxContext>();
        var attachment = await context.Set<Attachment>().FindAsync(attachmentId);
        if (attachment == null) return null;

        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(attachment.FileUrl);
        if (!response.IsSuccessStatusCode) return null;

        var ms = new MemoryStream();
        await response.Content.CopyToAsync(ms);
        ms.Position = 0;
        return ms;
    }

    private async Task<string> UploadToS3(string key, Stream inputStream, string folderClass, string bucket = "public", string mailId = "")
    {
        string bucketName = GetBucketName(bucket, key);
        string folderEnum = GetFolderEnum(folderClass, mailId);

        for (int i = 0; i < 3; i++)
        {
            try
            {
                return await ExecuteUploadAsync(inputStream, key, bucketName, folderEnum);
            }
            catch
            {
                if (i == 2) throw;
                inputStream.Position = 0;
            }
        }
        return JsonSerializer.Serialize(new { code = "400", msg = "重试上传失败" });
    }

    private async Task<string> ExecuteUploadAsync(Stream inputStream, string key, string bucketName, string folderEnum)
    {
        var client = _httpClientFactory.CreateClient();
        using var content = new MultipartFormDataContent();

        var streamContent = new StreamContent(inputStream);
        content.Add(streamContent, "file", key);

        string url = $"{ServerUrl}/api/files/{bucketName}/add?folderEnum={folderEnum}";
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = content };
        AddAuthHeaders(request);

        var result = await client.SendAsync(request);
        string responseText = await result.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseText);
        if (doc.RootElement.ValueKind == JsonValueKind.Array && doc.RootElement.GetArrayLength() > 0)
        {
            return doc.RootElement[0].ToString();
        }
        return responseText;
    }

    private async Task<string> GetFileUrl(string filePath, bool isPublic = true)
    {
        string bucketName = isPublic ? "492342278103371776-public" : "492342278103371776-private";
        if (filePath.Contains("oa_files")) bucketName = "topkeeoa";

        string url = $"{ServerUrl}/api/files/{bucketName}/get_file_url?filePath={System.Net.WebUtility.UrlEncode(filePath)}";
        var client = _httpClientFactory.CreateClient();
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        AddAuthHeaders(request);

        var result = await client.SendAsync(request);
        string responseText = await result.Content.ReadAsStringAsync();

        using var doc = JsonDocument.Parse(responseText);
        if (doc.RootElement.TryGetProperty("preSignedUrl", out var urlEl))
        {
            return urlEl.GetString() ?? "";
        }
        return "";
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        string token = Md5($"{ApiToken}-{DateTime.Now:yyyy-MM-dd}");
        request.Headers.TryAddWithoutValidation("token", token);
    }

    private static string GetBucketName(string bucket, string key) => bucket switch
    {
        "private" => "492342278103371776-private",
        "public" => "492342278103371776-public",
        "topkeeoa" => "topkeeoa",
        _ => key.Contains("oa_files") ? "topkeeoa" : "492342278103371776-private"
    };

    private static string GetFolderEnum(string folderClass, string mailId) => folderClass switch
    {
        "annex" => "1",
        "personal" => $"2&MailID={(string.IsNullOrEmpty(mailId) ? "0" : mailId)}",
        "okr" => "3",
        "ckFile" => "4",
        _ => ""
    };

    private static string Md5(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexStringLower(hash);
    }
}
