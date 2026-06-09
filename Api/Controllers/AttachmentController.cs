using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/attachments")]
public class AttachmentController(
    AttachmentStorageService storageService,
    CargoInboxContext context,
    ITenantProvider tenantProvider) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetThreadList()
    {
        var threads = await context.Conversations
            .AsNoTracking()
            .Where(c => c.Channel == MessageChannel.Email)
            .OrderByDescending(c => c.LastMessageAt)
            .Take(50)
            .Select(c => new
            {
                id = c.Id,
                senderName = c.Contact != null ? c.Contact.Name : (c.Messages.OrderByDescending(m => m.DateTime).Select(m => m.FromAddress).FirstOrDefault() ?? "未知发件人"),
                subject = c.Title,
                snippet = c.Messages.OrderByDescending(m => m.DateTime).Select(m => m.TextBody).FirstOrDefault() ?? c.Title,
                aiSummary = c.Status == MailStatus.Open ? (string?)null : "AI意向识别已激活",
                receivedAt = c.LastMessageAt.ToString("yyyy-MM-dd HH:mm"),
                channelType = "attachments",
                unreadCount = c.Status == MailStatus.Open ? 1 : 0,
                isLockedByOperator = false,
                lockedOperatorName = (string?)null
            })
            .ToListAsync();

        return Ok(threads);
    }
    [HttpPost]
    [RequestFormLimits(ValueLengthLimit = int.MaxValue, MultipartBodyLengthLimit = int.MaxValue)]
    [DisableRequestSizeLimit]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> Upload([FromForm(Name = "file")] IList<IFormFile> files, [FromQuery] string messageId)
    {
        if (files == null || files.Count == 0) return BadRequest(new { message = "未检测到上传的文件" });

        var resultList = new List<Attachment>();

        foreach (var file in files)
        {
            if (file.Length > 50 * 1024 * 1024)
                return BadRequest(new { message = $"文件 [{file.FileName}] 超过50MB限制" });

            var attachment = await storageService.StoreAsync(file.OpenReadStream(), file.FileName, file.ContentType, messageId);
            resultList.Add(attachment);
        }

        return Ok(resultList);
    }

    [HttpGet("{id}/download")]
    public async Task<IActionResult> Download(string id)
    {
        var attachment = await context.Attachments.AsNoTracking().FirstOrDefaultAsync(a => a.Id == id);
        if (attachment == null) return NotFound(new { message = "附件不存在" });

        if (!string.IsNullOrEmpty(attachment.FileUrl))
            return Redirect(attachment.FileUrl);

        var stream = await storageService.RetrieveAsync(id);
        if (stream == null) return NotFound(new { message = "文件已过期或不存在" });

        return File(stream, attachment.ContentType, attachment.FileName);
    }
}
