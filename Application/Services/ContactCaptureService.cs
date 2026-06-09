using System.Text.RegularExpressions;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class ContactCaptureService(CargoInboxContext context)
{
    private static readonly Regex EmailRegex = new(
        @"[\w.+-]+@[\w.-]+\.\w+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string? ExtractEmailAddress(string? addressHeader)
    {
        if (string.IsNullOrWhiteSpace(addressHeader)) return null;
        var match = EmailRegex.Match(addressHeader);
        return match.Success ? match.Value.ToLowerInvariant() : null;
    }

    public Task<string> GetOrCreateContactAsync(string email, string? tenantId = null)
        => GetOrCreateContactAsync(email, tenantId, CancellationToken.None);

    public async Task<string> GetOrCreateContactAsync(string email, string? tenantId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email)) return await GetDefaultContactIdAsync(tenantId);

        var normalized = email.ToLower();
        var query = context.Contacts.Where(c => c.Email != null && c.Email.ToLower() == normalized);
        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(c => c.TenantId == tenantId || c.TenantId == "");

        var existing = await query.FirstOrDefaultAsync(cancellationToken);
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(tenantId) && string.IsNullOrEmpty(existing.TenantId))
            {
                existing.TenantId = tenantId;
                await context.SaveChangesAsync(cancellationToken);
            }
            return existing.Id;
        }

        var newContact = new Contact
        {
            TenantId = tenantId ?? "",
            Email = normalized,
            Name = email.Split('@')[0],
            Company = "独立往来企业",
            Notes = "Front 收件箱流水线自动捕获创建",
            UpdatedAt = DateTime.UtcNow
        };

        context.Contacts.Add(newContact);
        await context.SaveChangesAsync(cancellationToken);
        return newContact.Id;
    }

    public async Task<string> GetOrCreateContactByPhoneAsync(string phone, string? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(phone)) return await GetDefaultContactIdAsync(tenantId);

        var query = context.Contacts.Where(c => c.Phone == phone);
        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(c => c.TenantId == tenantId || c.TenantId == "");

        var existing = await query.FirstOrDefaultAsync();
        if (existing != null)
        {
            if (!string.IsNullOrEmpty(tenantId) && string.IsNullOrEmpty(existing.TenantId))
            {
                existing.TenantId = tenantId;
                await context.SaveChangesAsync();
            }
            return existing.Id;
        }

        var suffix = phone.Length >= 6 ? phone[^6..] : phone;
        var newContact = new Contact
        {
            TenantId = tenantId ?? "",
            Phone = phone,
            Name = $"WhatsApp 用户 {suffix}",
            Company = "WhatsApp 客户",
            Notes = "WhatsApp 入站自动创建"
        };

        context.Contacts.Add(newContact);
        await context.SaveChangesAsync();
        return newContact.Id;
    }

    public async Task<string> GetOrCreateLiveChatVisitorAsync(
        string visitorId,
        string? name,
        string? email,
        string tenantId)
    {
        if (string.IsNullOrWhiteSpace(visitorId))
            return await GetDefaultContactIdAsync(tenantId);

        var existing = await context.Contacts
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c =>
                c.LiveChatVisitorId == visitorId
                && (c.TenantId == tenantId || c.TenantId == ""));

        if (existing != null)
        {
            var changed = false;
            if (!string.IsNullOrEmpty(tenantId) && string.IsNullOrEmpty(existing.TenantId))
            {
                existing.TenantId = tenantId;
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(name) && existing.Name.StartsWith("Visitor "))
            {
                existing.Name = name.Trim();
                changed = true;
            }
            if (!string.IsNullOrWhiteSpace(email) && string.IsNullOrEmpty(existing.Email))
            {
                existing.Email = email.Trim().ToLower();
                changed = true;
            }
            if (changed)
            {
                existing.UpdatedAt = DateTime.UtcNow;
                await context.SaveChangesAsync();
            }
            return existing.Id;
        }

        var displayName = !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : $"Visitor {visitorId[..Math.Min(8, visitorId.Length)]}";

        var contact = new Contact
        {
            TenantId = tenantId,
            LiveChatVisitorId = visitorId,
            Name = displayName,
            Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLower(),
            LeadSource = "LiveChat",
            Notes = "Live chat widget visitor",
            UpdatedAt = DateTime.UtcNow
        };

        context.Contacts.Add(contact);
        await context.SaveChangesAsync();
        return contact.Id;
    }

    private async Task<string> GetDefaultContactIdAsync(string? tenantId)
    {
        var query = context.Contacts.Where(c => c.Email == "anonymous@cargoinbox.cn");
        if (!string.IsNullOrEmpty(tenantId))
            query = query.Where(c => c.TenantId == tenantId || c.TenantId == "");

        var anonymous = await query.FirstOrDefaultAsync();
        if (anonymous == null)
        {
            anonymous = new Contact
            {
                TenantId = tenantId ?? "",
                Email = "anonymous@cargoinbox.cn",
                Name = "Anonymous"
            };
            context.Contacts.Add(anonymous);
            await context.SaveChangesAsync();
        }
        return anonymous.Id;
    }
}
