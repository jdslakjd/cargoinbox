namespace CargoInbox.Application.Services;

public interface ITenantProvider
{
    string TenantId { get; }
}

public class TenantProvider : ITenantProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private const string DefaultTenant = "default";

    public TenantProvider(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string TenantId
    {
        get
        {
            var claim = _httpContextAccessor.HttpContext?.User?.FindFirst("tenant_id");
            return claim?.Value ?? DefaultTenant;
        }
    }
}
