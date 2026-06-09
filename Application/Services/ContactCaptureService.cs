using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class ContactCaptureService(CargoInboxContext context)
{
    public async Task<string> GetOrCreateContactAsync(string email, string? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(email)) return await GetDefaultContactIdAsync(tenantId);

        var normalized = email.ToLower();
        var query = context.Contacts.Where(c => c.Email != null && c.Email.ToLower() == normalized);
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

        var newContact = new Contact
        {
            TenantId = tenantId ?? "",
            Email = normalized,
            Name = email.Split('@')[0],
            Company = "独立往来企业",
            Notes = "Front 收件箱流水线自动捕获创建"
        };

        context.Contacts.Add(newContact);
        await context.SaveChangesAsync();
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
