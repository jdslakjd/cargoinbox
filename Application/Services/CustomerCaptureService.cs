using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

public class CustomerCaptureService(CargoInboxContext context)
{
    public async Task<string> GetOrCreateCustomerAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email)) return await GetDefaultSystemCustomerIdAsync();

        var existingCustomer = await context.Customers.FirstOrDefaultAsync(c => c.Email.ToLower() == email.ToLower());
        if (existingCustomer != null) return existingCustomer.Id;

        var newCustomer = new Customer
        {
            Id = Guid.NewGuid().ToString("N"),
            Email = email.ToLower(),
            ContactName = email.Split('@')[0],
            CompanyName = "待跟进跨境企业",
            LifecycleStatus = "Lead",
            CreatedAt = DateTime.UtcNow
        };

        context.Customers.Add(newCustomer);
        await context.SaveChangesAsync();
        return newCustomer.Id;
    }

    private async Task<string> GetDefaultSystemCustomerIdAsync()
    {
        var anonymous = await context.Customers.FirstOrDefaultAsync(c => c.Email == "anonymous@cargoinbox.cn");
        if (anonymous == null)
        {
            anonymous = new Customer
            {
                Email = "anonymous@cargoinbox.cn",
                ContactName = "Anonymous",
                LifecycleStatus = "Lead"
            };
            context.Customers.Add(anonymous);
            await context.SaveChangesAsync();
        }
        return anonymous.Id;
    }
}
