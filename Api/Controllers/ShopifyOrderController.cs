using System.Security.Claims;
using CargoInbox.Application.Services;
using CargoInbox.Core.Entities;
using CargoInbox.Infrastructure.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CargoInbox.Api.Controllers;

[Authorize]
[ApiController]
[Route("api/ecommerce/shopify")]
public class ShopifyOrderController(CargoInboxContext context, ITenantProvider tenantProvider) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";

    [HttpGet("orders")]
    public async Task<IActionResult> GetCustomerOrders([FromQuery] string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            return BadRequest(new { error = "客户邮箱不能为空" });

        var orders = await context.Set<ShopifyOrder>()
            .AsNoTracking()
            .Where(o => o.CustomerEmail == email && o.TenantId == tenantProvider.TenantId)
            .OrderByDescending(o => o.CreatedAtUtc)
            .Take(50)
            .ToListAsync();

        return Ok(orders);
    }

    [HttpPost("orders")]
    public async Task<IActionResult> CreateOrder([FromBody] ShopifyOrder order)
    {
        order.Id = Guid.NewGuid().ToString("N");
        order.TenantId = tenantProvider.TenantId;
        context.Set<ShopifyOrder>().Add(order);

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "Agent",
            Action = "EcommerceOrderCreated",
            Detail = $"创建订单 {order.OrderNumber}，金额 {order.TotalPrice} {order.Currency}",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { success = true, orderId = order.Id });
    }

    [HttpPost("orders/{id}/refund")]
    public async Task<IActionResult> TriggerRefund(string id)
    {
        var order = await context.Set<ShopifyOrder>().FindAsync(id);
        if (order == null) return NotFound();

        order.FulfillmentStatus = "Refunded";

        context.ActivityLogs.Add(new ActivityLog
        {
            UserId = CurrentUserId,
            UserName = User.FindFirstValue(ClaimTypes.Name) ?? "Agent",
            Action = "EcommerceRefund",
            Detail = $"对订单 {order.OrderNumber} 发起退款",
            TenantId = tenantProvider.TenantId
        });

        await context.SaveChangesAsync();
        return Ok(new { success = true, currentStatus = order.FulfillmentStatus });
    }
}
