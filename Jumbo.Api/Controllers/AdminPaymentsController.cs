using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>Admin, read-only: the latest orders of the Payme payment with the state of their Payme transaction.</summary>
[ApiController]
[Route("api/admin/payments")]
[RequireRole(UserRole.Admin)]
public class AdminPaymentsController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<AdminPaymentDto>> List([FromQuery] int take = 200)
    {
        take = Math.Clamp(take, 1, 500);
        var orders = await db.PaymentOrders.AsNoTracking().OrderByDescending(o => o.PaymentOrderId).Take(take).ToListAsync();
        var ids = orders.Select(o => o.PaymentOrderId).ToList();
        var users = orders.Where(o => o.UserId != null).Select(o => o.UserId!.Value).Distinct().ToList();

        var names = await db.Users.AsNoTracking().Where(u => users.Contains(u.UserId)).ToDictionaryAsync(u => u.UserId, u => u.Username);
        var transactions = (await db.PaymeTransactions.AsNoTracking().Where(t => ids.Contains(t.PaymentOrderId)).ToListAsync())
            .GroupBy(t => t.PaymentOrderId).ToDictionary(g => g.Key, g => g.OrderByDescending(t => t.PaymeTransactionId).First());

        return orders.Select(o =>
        {
            transactions.TryGetValue(o.PaymentOrderId, out var t);
            return new AdminPaymentDto
            {
                OrderId = o.PaymentOrderId, UserId = o.UserId, UserName = o.UserId is { } u && names.TryGetValue(u, out var n) ? n : null,
                Plan = o.Plan, Period = o.Period, AmountSum = o.AmountSum, Status = o.Status, CreatedAt = o.CreatedAt, PaidAt = o.PaidAt,
                PaymeId = t?.PaymeId, PaymeState = t?.State, PaymeReason = t?.Reason,
            };
        }).ToList();
    }
}
