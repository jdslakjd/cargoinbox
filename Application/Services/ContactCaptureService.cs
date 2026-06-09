using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class ContactCaptureService(CargoInboxContext context)
{
    public async Task<string> GetOrCreateContactAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return await GetDefaultContactIdAsync();

        var existing = await context.Contacts.FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == email.ToLower());
        if (existing != null) return existing.Id;

        var newContact = new Contact
        {
            Email = email.ToLower(),
            Name = email.Split('@')[0],
            Company = "独立往来企业",
            Notes = "Front 收件箱流水线自动捕获创建"
        };

        context.Contacts.Add(newContact);
        await context.SaveChangesAsync();
        return newContact.Id;
    }

    public async Task<string> GetOrCreateContactByPhoneAsync(string phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return await GetDefaultContactIdAsync();

        var existing = await context.Contacts.FirstOrDefaultAsync(c => c.Phone == phone);
        if (existing != null) return existing.Id;

        var newContact = new Contact
        {
            Phone = phone,
            Name = $"WhatsApp 用户 {phone[^6..]}",
            Company = "WhatsApp 客户",
            Notes = "WhatsApp 入站自动创建"
        };

        context.Contacts.Add(newContact);
        await context.SaveChangesAsync();
        return newContact.Id;
    }

    private async Task<string> GetDefaultContactIdAsync()
    {
        var anonymous = await context.Contacts.FirstOrDefaultAsync(c => c.Email == "anonymous@cargoinbox.cn");
        if (anonymous == null)
        {
            anonymous = new Contact { Email = "anonymous@cargoinbox.cn", Name = "Anonymous" };
            context.Contacts.Add(anonymous);
            await context.SaveChangesAsync();
        }
        return anonymous.Id;
    }
}
