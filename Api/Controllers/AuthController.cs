using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using CargoInbox.Application.Services;

namespace CargoInbox.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(CargoInboxContext context, IConfiguration configuration) : ControllerBase
{
    [HttpPost("register-tenant")]
    public async Task<IActionResult> RegisterTenant([FromBody] RegisterTenantRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.TenantName) || string.IsNullOrWhiteSpace(request.Subdomain))
            return BadRequest(new { message = "租户名称和子域不能为空" });

        var exists = await context.Set<Tenant>().AnyAsync(t => t.Id == request.Subdomain);
        if (exists) return BadRequest(new { message = "该子域已被占用" });

        var tenant = new Tenant
        {
            Id = request.Subdomain,
            Name = request.TenantName,
            Domain = request.Subdomain,
            IsActive = true
        };
        context.Set<Tenant>().Add(tenant);

        var usernameExists = await context.Users.IgnoreQueryFilters()
            .AnyAsync(u => u.Username == request.AdminUsername && u.TenantId == request.Subdomain);
        if (usernameExists) return BadRequest(new { message = "管理员用户名已存在" });

        var admin = new User
        {
            TenantId = request.Subdomain,
            Username = request.AdminUsername,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.AdminPassword),
            DisplayName = request.DisplayName,
            Role = UserRole.Admin,
            Email = request.AdminEmail
        };
        context.Users.Add(admin);

        await context.SaveChangesAsync();
        return Ok(new { message = "租户空间和超级管理员创建成功", tenantId = tenant.Id, subdomain = request.Subdomain });
    }

    [Authorize]
    [HttpPost("register-user")]
    public async Task<IActionResult> RegisterUser([FromBody] RegisterRequest request)
    {
        if (!InboxPermissionService.IsAdmin(User))
            return Forbid();

        var tenantId = User.FindFirstValue("tenant_id") ?? request.TenantId;
        if (string.IsNullOrWhiteSpace(tenantId))
            return BadRequest(new { message = "租户ID不能为空" });

        var tenantExists = await context.Set<Tenant>().AnyAsync(t => t.Id == tenantId);
        if (!tenantExists) return BadRequest(new { message = "租户不存在" });

        var exists = await context.Users.AnyAsync(u => u.Username == request.Username && u.TenantId == tenantId);
        if (exists) return BadRequest(new { message = "用户名已存在" });

        var user = new User
        {
            TenantId = tenantId,
            Username = request.Username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Role = request.IsAdmin ? UserRole.Admin : UserRole.User,
            Email = request.Email
        };

        context.Users.Add(user);
        await context.SaveChangesAsync();
        return Ok(new { message = "注册成功" });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var query = context.Users.IgnoreQueryFilters().AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.TenantId))
            query = query.Where(u => u.TenantId == request.TenantId);

        var user = await query.FirstOrDefaultAsync(u => u.Username == request.Username);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            return Unauthorized(new { message = "用户名或密码错误" });

        if (!user.IsActive) return Forbid();

        var tenant = await context.Set<Tenant>().AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId);

        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtKey = configuration["Jwt:Key"] ?? "CargoInbox_Super_Secret_Security_Key_2026_Top_Secret";
        var key = Encoding.UTF8.GetBytes(jwtKey);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.Username),
                new Claim(ClaimTypes.Role, user.Role.ToString()),
                new Claim("tenant_id", user.TenantId),
                new Claim("timezone", user.Timezone),
                new Claim("locale", user.Locale)
            ]),
            Expires = DateTime.UtcNow.AddDays(7),
            Issuer = configuration["Jwt:Issuer"] ?? "CargoInboxServer",
            Audience = configuration["Jwt:Audience"] ?? "CargoInboxClient",
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return Ok(new
        {
            token = tokenHandler.WriteToken(token),
            userId = user.Id,
            role = user.Role.ToString(),
            displayName = user.DisplayName,
            email = user.Email,
            tenantId = user.TenantId,
            tenantName = tenant?.Name
        });
    }
}

public record RegisterTenantRequest(string TenantName, string Subdomain, string AdminUsername, string AdminPassword, string DisplayName, string? AdminEmail = null);
public record RegisterRequest(string TenantId, string Username, string Password, string DisplayName, bool IsAdmin, string? Email = null);
public record LoginRequest(string Username, string Password, string? TenantId);
