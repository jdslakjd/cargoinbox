using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using CargoInbox.Application.Services;

namespace CargoInbox.Infrastructure.Data;

public class CargoInboxContextFactory : IDesignTimeDbContextFactory<CargoInboxContext>
{
    public CargoInboxContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json")
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<CargoInboxContext>();
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=cargoinbox_db;Username=postgres;Password=your_password";
        optionsBuilder.UseNpgsql(connectionString, o => o.UseVector());

        var tenantProvider = new TenantProvider(new HttpContextAccessor());
        return new CargoInboxContext(optionsBuilder.Options, tenantProvider);
    }
}

internal class HttpContextAccessor : IHttpContextAccessor
{
    public HttpContext? HttpContext { get => null; set { } }
}
