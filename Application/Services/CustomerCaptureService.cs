using CargoInbox.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Application.Services;

/// <summary>
/// Legacy facade — all new code should use <see cref="ContactCaptureService"/>.
/// Returns Contact Id (unified CRM profile).
/// </summary>
public class CustomerCaptureService(ContactCaptureService contacts)
{
    public Task<string> GetOrCreateCustomerAsync(string email, string? tenantId = null)
        => contacts.GetOrCreateContactAsync(email, tenantId);
}
