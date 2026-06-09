using System.Security.Claims;
using CargoInbox.Application.DTOs;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/mails/{id}")]
public class MailCollaborationController(CargoInboxContext context) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? User.FindFirstValue(ClaimTypes.Email) ?? "Anonymous";

    [HttpGet("detail")]
    public async Task<IActionResult> GetMailDetail(string id)
    {
        var mail = await context.Mails
            .AsNoTracking()
            .Include(m => m.Comments.OrderBy(c => c.CreatedAt))
            .FirstOrDefaultAsync(m => m.Id == id);

        if (mail == null) return NotFound(new { message = "邮件未找到" });

        var dto = new MailDetailDto(
            mail.Id,
            mail.FromAddress,
            mail.ToAddress,
            mail.Subject,
            mail.TextBody,
            mail.HtmlBody,
            mail.DateTime,
            mail.IsRead,
            mail.Status,
            mail.AssignedToUserId,
            mail.Comments.Select(c => new MailCommentDto(c.Id, c.MailId, c.UserId, c.UserName, c.Content, c.CreatedAt)).ToList()
        );

        return Ok(dto);
    }

    [HttpPost("assign")]
    public async Task<IActionResult> AssignMail(string id, [FromBody] AssignMailRequest request)
    {
        var mail = await context.Mails.FirstOrDefaultAsync(m => m.Id == id);
        if (mail == null) return NotFound(new { message = "邮件未找到" });

        mail.Status = MailStatus.Assigned;
        mail.AssignedToUserId = request.AssignedToUserId;
        mail.AssignedAt = DateTime.UtcNow;

        var systemComment = new MailComment
        {
            MailId = mail.Id,
            UserId = "system",
            UserName = "系统通知",
            Content = $"{CurrentUserName} 将该邮件指派给了用户 [{request.AssignedToUserId}]"
        };
        context.MailComments.Add(systemComment);

        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            MailId = mail.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Assigned",
            Detail = $"指派给 {request.AssignedToUserId}"
        });
        await context.SaveChangesAsync();
        return Ok(new { message = "邮件指派成功", status = mail.Status, assignedTo = mail.AssignedToUserId });
    }

    [HttpPost("unassign")]
    public async Task<IActionResult> UnassignMail(string id)
    {
        var mail = await context.Mails.FirstOrDefaultAsync(m => m.Id == id);
        if (mail == null) return NotFound(new { message = "邮件未找到" });

        mail.Status = MailStatus.Open;
        mail.AssignedToUserId = null;
        mail.AssignedAt = null;

        var systemComment = new MailComment
        {
            MailId = mail.Id,
            UserId = "system",
            UserName = "系统通知",
            Content = $"{CurrentUserName} 取消了该邮件的指派，邮件已退回公海池"
        };
        context.MailComments.Add(systemComment);

        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            MailId = mail.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Unassigned",
            Detail = "取消指派"
        });
        await context.SaveChangesAsync();
        return Ok(new { message = "已取消指派", status = mail.Status });
    }

    [HttpPost("archive")]
    public async Task<IActionResult> ArchiveMail(string id)
    {
        var mail = await context.Mails.FirstOrDefaultAsync(m => m.Id == id);
        if (mail == null) return NotFound(new { message = "邮件未找到" });

        mail.Status = MailStatus.Archived;

        var systemComment = new MailComment
        {
            MailId = mail.Id,
            UserId = "system",
            UserName = "系统通知",
            Content = $"{CurrentUserName} 归档了该邮件"
        };
        context.MailComments.Add(systemComment);

        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            MailId = mail.Id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Archived",
            Detail = "归档邮件"
        });
        await context.SaveChangesAsync();
        return Ok(new { message = "邮件已归档", status = mail.Status });
    }

    [HttpPost("comments")]
    public async Task<IActionResult> AddComment(string id, [FromBody] CreateCommentRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Content))
            return BadRequest(new { message = "评论内容不能为空" });

        var mailExists = await context.Mails.AnyAsync(m => m.Id == id);
        if (!mailExists) return NotFound(new { message = "邮件未找到" });

        var comment = new MailComment
        {
            MailId = id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Content = request.Content
        };

        context.MailComments.Add(comment);
        await context.SaveChangesAsync();

        context.ActivityLogs.Add(new ActivityLog
        {
            MailId = id,
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "Commented",
            Detail = $"评论: {(request.Content.Length > 60 ? request.Content[..60] + "..." : request.Content)}"
        });
        await context.SaveChangesAsync();
        return Ok(new MailCommentDto(comment.Id, comment.MailId, comment.UserId, comment.UserName, comment.Content, comment.CreatedAt));
    }
}
