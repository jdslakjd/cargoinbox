using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/settings")]
public class InboxSettingsController(
    CargoInboxContext context,
    ITenantProvider tenantProvider,
    InboxPermissionService inboxPermissionService) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system-user";
    private string CurrentUserName => User.FindFirstValue(ClaimTypes.Name) ?? "Anonymous";
    private bool IsAdmin => InboxPermissionService.IsAdmin(User);

    public record BindEmailRequest(
        string EmailAddress, string PasswordOrToken, MailProviderType ProviderType,
        string ImapHost, int ImapPort, bool ImapUseSsl,
        string SmtpHost, int SmtpPort, bool SmtpUseSsl
    );

    [HttpGet("mailboxes")]
    public async Task<IActionResult> GetMailboxes()
    {
        var configs = await context.UserMailConfigs
            .AsNoTracking()
            .Where(c => c.UserId == CurrentUserId)
            .OrderByDescending(c => c.IsSuspended)
            .ThenBy(c => c.EmailAddress)
            .Select(c => new
            {
                c.Id,
                c.EmailAddress,
                c.ProviderType,
                c.ImapHost,
                c.ImapPort,
                c.ImapUseSsl,
                c.SmtpHost,
                c.SmtpPort,
                c.SmtpUseSsl,
                c.IsSuspended,
                c.ConsecutiveFailureCount,
                c.LastSyncUid,
                c.SharedInboxId
            })
            .ToListAsync();
        return Ok(configs);
    }

    [HttpDelete("mailboxes/{id}")]
    public async Task<IActionResult> DeleteMailbox(string id)
    {
        var config = await context.UserMailConfigs
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == CurrentUserId);
        if (config == null) return NotFound(new { message = "邮箱配置不存在" });

        if (config.SharedInboxId != null)
        {
            var shared = await context.SharedInboxes.FindAsync(config.SharedInboxId);
            if (shared != null) context.SharedInboxes.Remove(shared);
        }

        context.UserMailConfigs.Remove(config);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "UnbindMailbox",
            Detail = $"解绑邮箱: {config.EmailAddress}",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { message = "邮箱已解绑" });
    }
    [HttpPost]
    public async Task<IActionResult> BindAndValidateEmail([FromBody] BindEmailRequest request)
    {
        bool isImapValid = false;
        string imapError = string.Empty;
        using (var imapClient = new ImapClient())
        {
            try
            {
                await imapClient.ConnectAsync(request.ImapHost, request.ImapPort, request.ImapUseSsl);
                await imapClient.AuthenticateAsync(request.EmailAddress, request.PasswordOrToken);
                isImapValid = imapClient.IsAuthenticated;
                await imapClient.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                imapError = ex.Message;
            }
        }

        if (!isImapValid)
            return BadRequest(new { error = $"IMAP 入站收信验证失败。错误: {imapError}" });

        bool isSmtpValid = false;
        string smtpError = string.Empty;
        using (var smtpClient = new SmtpClient())
        {
            try
            {
                await smtpClient.ConnectAsync(request.SmtpHost, request.SmtpPort, request.SmtpUseSsl);
                await smtpClient.AuthenticateAsync(request.EmailAddress, request.PasswordOrToken);
                isSmtpValid = smtpClient.IsAuthenticated;
                await smtpClient.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                smtpError = ex.Message;
            }
        }

        if (!isSmtpValid)
            return BadRequest(new { error = $"SMTP 出站发信验证失败。错误: {smtpError}" });

        var sharedInbox = new SharedInbox
        {
            Name = $"{request.EmailAddress} ({request.ProviderType})",
            Description = "物理全量激活的自动收发中枢",
            EmailAddress = request.EmailAddress,
            ImapHost = request.ImapHost,
            ImapPort = request.ImapPort,
            ImapUseSsl = request.ImapUseSsl,
            SmtpHost = request.SmtpHost,
            SmtpPort = request.SmtpPort,
            SmtpUseSsl = request.SmtpUseSsl,
            ProviderType = request.ProviderType,
            EncryptedPassword = request.PasswordOrToken,
            TenantId = tenantProvider.TenantId
        };
        context.SharedInboxes.Add(sharedInbox);
        await context.SaveChangesAsync();

        var mailConfig = new UserMailConfig
        {
            UserId = CurrentUserId,
            SharedInboxId = sharedInbox.Id,
            EmailAddress = request.EmailAddress,
            ImapHost = request.ImapHost,
            ImapPort = request.ImapPort,
            ImapUseSsl = request.ImapUseSsl,
            SmtpHost = request.SmtpHost,
            SmtpPort = request.SmtpPort,
            SmtpUseSsl = request.SmtpUseSsl,
            ProviderType = request.ProviderType,
            EncryptedAppPassword = request.PasswordOrToken,
            TenantId = tenantProvider.TenantId
        };
        context.UserMailConfigs.Add(mailConfig);

        context.UserInboxPermissions.Add(new UserInboxPermission
        {
            TenantId = tenantProvider.TenantId,
            UserId = CurrentUserId,
            SharedInboxId = sharedInbox.Id
        });

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "BindMailbox",
            Detail = $"成功绑定 {request.ProviderType} 渠道邮箱: {request.EmailAddress}，双向连通性测试通过。",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();

        return Ok(new { success = true, inboxId = sharedInbox.Id, message = "邮箱物理绑定成功，后台常驻 Worker 已接管增量拉取任务。" });
    }

    [HttpGet("shared-inboxes")]
    public async Task<IActionResult> GetSharedInboxes()
    {
        var allowedIds = await inboxPermissionService.GetAllowedSharedInboxIdsAsync(
            CurrentUserId, tenantProvider.TenantId, IsAdmin);

        var query = context.SharedInboxes
            .AsNoTracking()
            .Where(s => s.TenantId == tenantProvider.TenantId && s.IsActive);

        if (!IsAdmin)
            query = query.Where(s => allowedIds.Contains(s.Id));

        var inboxes = await query.OrderBy(s => s.Name).ToListAsync();
        var inboxIds = inboxes.Select(s => s.Id).ToList();

        var counts = await context.Conversations
            .AsNoTracking()
            .Where(c => c.SharedInboxId != null && inboxIds.Contains(c.SharedInboxId)
                && c.Status != MailStatus.Trash && c.Status != MailStatus.Spam)
            .GroupBy(c => c.SharedInboxId!)
            .Select(g => new { SharedInboxId = g.Key, Count = g.Count() })
            .ToListAsync();

        var countMap = counts.ToDictionary(x => x.SharedInboxId, x => x.Count);

        return Ok(inboxes.Select(s => new
        {
            s.Id,
            s.Name,
            s.EmailAddress,
            s.Description,
            conversationCount = countMap.GetValueOrDefault(s.Id, 0)
        }));
    }

    [HttpGet("signatures")]
    public async Task<IActionResult> GetSignatures()
    {
        var sigs = await context.UserSignatures
            .AsNoTracking()
            .Where(s => s.UserId == CurrentUserId)
            .OrderByDescending(s => s.IsDefault)
            .ToListAsync();
        return Ok(sigs);
    }

    [HttpPost("signatures")]
    public async Task<IActionResult> CreateSignature([FromBody] UserSignature signature)
    {
        signature.Id = Guid.NewGuid().ToString("N");
        signature.UserId = CurrentUserId;
        if (signature.IsDefault)
        {
            var existingDefaults = await context.UserSignatures
                .Where(s => s.UserId == CurrentUserId && s.IsDefault)
                .ToListAsync();
            foreach (var s in existingDefaults) s.IsDefault = false;
        }
        context.UserSignatures.Add(signature);
        await context.SaveChangesAsync();
        return Ok(signature);
    }

    [HttpPut("signatures/{id}")]
    public async Task<IActionResult> UpdateSignature(string id, [FromBody] UserSignature updated)
    {
        var sig = await context.UserSignatures.FirstOrDefaultAsync(s => s.Id == id && s.UserId == CurrentUserId);
        if (sig == null) return NotFound();
        sig.Name = updated.Name;
        sig.HtmlContent = updated.HtmlContent;
        sig.IsDefault = updated.IsDefault;
        if (sig.IsDefault)
        {
            var others = await context.UserSignatures
                .Where(s => s.UserId == CurrentUserId && s.Id != id && s.IsDefault)
                .ToListAsync();
            foreach (var s in others) s.IsDefault = false;
        }
        await context.SaveChangesAsync();
        return Ok(sig);
    }

    [HttpDelete("signatures/{id}")]
    public async Task<IActionResult> DeleteSignature(string id)
    {
        var sig = await context.UserSignatures.FirstOrDefaultAsync(s => s.Id == id && s.UserId == CurrentUserId);
        if (sig == null) return NotFound();
        context.UserSignatures.Remove(sig);
        await context.SaveChangesAsync();
        return Ok(new { message = "签名已删除" });
    }

    [HttpGet("views")]
    public async Task<IActionResult> GetViews()
    {
        var views = await context.SavedViews
            .AsNoTracking()
            .Where(v => v.UserId == CurrentUserId)
            .OrderBy(v => v.SortOrder)
            .ToListAsync();
        return Ok(views);
    }

    [HttpPost("views")]
    public async Task<IActionResult> CreateView([FromBody] SavedView view)
    {
        view.Id = Guid.NewGuid().ToString("N");
        view.UserId = CurrentUserId;
        context.SavedViews.Add(view);
        await context.SaveChangesAsync();
        return Ok(view);
    }

    [HttpPut("views/{id}")]
    public async Task<IActionResult> UpdateView(string id, [FromBody] SavedView updated)
    {
        var view = await context.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == CurrentUserId);
        if (view == null) return NotFound();
        view.Name = updated.Name;
        view.FilterJson = updated.FilterJson;
        view.IsPinned = updated.IsPinned;
        view.SortOrder = updated.SortOrder;
        await context.SaveChangesAsync();
        return Ok(view);
    }

    [HttpDelete("views/{id}")]
    public async Task<IActionResult> DeleteView(string id)
    {
        var view = await context.SavedViews.FirstOrDefaultAsync(v => v.Id == id && v.UserId == CurrentUserId);
        if (view == null) return NotFound();
        context.SavedViews.Remove(view);
        await context.SaveChangesAsync();
        return Ok(new { message = "视图已删除" });
    }

    [HttpGet("groups")]
    public async Task<IActionResult> GetGroups()
    {
        var groups = await context.TeamGroups
            .AsNoTracking()
            .OrderBy(g => g.Name)
            .ToListAsync();
        return Ok(groups);
    }

    [HttpPost("groups")]
    public async Task<IActionResult> CreateGroup([FromBody] TeamGroup group)
    {
        group.Id = Guid.NewGuid().ToString("N");
        context.TeamGroups.Add(group);
        await context.SaveChangesAsync();
        return Ok(group);
    }

    [HttpGet("team")]
    public async Task<IActionResult> GetTeamMembers()
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantProvider.TenantId && u.IsActive)
            .OrderBy(u => u.DisplayName)
            .Select(u => new { u.Id, u.Username, u.DisplayName, u.Email, u.Role, u.CreatedAt })
            .ToListAsync();
        return Ok(users);
    }

    public record InviteTeamMemberRequest(
        string Username, string Password, string DisplayName, string? Email, bool IsAdmin = false);

    [HttpPost("team/invite")]
    public async Task<IActionResult> InviteTeamMember([FromBody] InviteTeamMemberRequest request)
    {
        var role = User.FindFirstValue(ClaimTypes.Role);
        if (role != UserRole.Admin.ToString())
            return Forbid();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { message = "用户名和密码不能为空" });

        var exists = await context.Users.AnyAsync(u =>
            u.Username == request.Username && u.TenantId == tenantProvider.TenantId);
        if (exists) return BadRequest(new { message = "用户名已存在" });

        var user = new User
        {
            TenantId = tenantProvider.TenantId,
            Username = request.Username.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = string.IsNullOrWhiteSpace(request.DisplayName) ? request.Username.Trim() : request.DisplayName.Trim(),
            Role = request.IsAdmin ? UserRole.Admin : UserRole.User,
            Email = request.Email?.Trim()
        };

        context.Users.Add(user);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "InviteTeamMember",
            Detail = $"邀请队友 {user.Username} ({user.Role})",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { message = "队友已添加", userId = user.Id });
    }

    public record SetInboxPermissionsRequest(List<string> SharedInboxIds);

    [HttpGet("team/{userId}/inbox-permissions")]
    public async Task<IActionResult> GetUserInboxPermissions(string userId)
    {
        if (!IsAdmin) return Forbid();

        var user = await context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantProvider.TenantId);
        if (user == null) return NotFound();

        var granted = await inboxPermissionService.GetGrantedInboxIdsAsync(userId, tenantProvider.TenantId);
        var mailLinked = await context.UserMailConfigs.AsNoTracking()
            .Where(c => c.UserId == userId && c.SharedInboxId != null)
            .Select(c => c.SharedInboxId!)
            .ToListAsync();

        var inboxes = await context.SharedInboxes.AsNoTracking()
            .Where(s => s.TenantId == tenantProvider.TenantId && s.IsActive)
            .OrderBy(s => s.Name)
            .Select(s => new { s.Id, s.Name, s.EmailAddress })
            .ToListAsync();

        return Ok(new
        {
            userId,
            grantedInboxIds = granted,
            mailboxLinkedInboxIds = mailLinked,
            availableInboxes = inboxes
        });
    }

    [HttpPut("team/{userId}/inbox-permissions")]
    public async Task<IActionResult> SetUserInboxPermissions(string userId, [FromBody] SetInboxPermissionsRequest request)
    {
        if (!IsAdmin) return Forbid();

        var user = await context.Users
            .FirstOrDefaultAsync(u => u.Id == userId && u.TenantId == tenantProvider.TenantId);
        if (user == null) return NotFound();

        await inboxPermissionService.SetUserInboxPermissionsAsync(
            userId, tenantProvider.TenantId, request.SharedInboxIds ?? []);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = CurrentUserName,
            Action = "SetInboxPermissions",
            Detail = $"更新 {user.Username} 的共享收件箱权限 ({request.SharedInboxIds?.Count ?? 0} 项)",
            TenantId = tenantProvider.TenantId
        });
        await context.SaveChangesAsync();

        return Ok(new { message = "权限已更新" });
    }
}
