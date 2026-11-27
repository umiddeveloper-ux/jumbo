using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Api.Services.Payme;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>
/// The orders of the site for the Payme payment (variant A): the learner picks a version and a period, the API makes an order with the price from
/// the plan prices and returns the Payme page to go to. The amount never comes from the client. Payme itself calls api/payme (PaymeController).
/// </summary>
[ApiController]
[Route("api/payments")]
public class PaymentsController(AppDbContext db, ICurrentUserService currentUser, IConfiguration config, TimeProvider clock) : ControllerBase
{
    private const int MaxOrdersPerDay = 30;

    /// <summary>Is the payment switched on (the site shows the button only then).</summary>
    [HttpGet("config")]
    public PaymentsConfigDto GetConfig()
    {
        var opts = PaymeOptions.From(config);
        Response.Headers.CacheControl = "no-store";
        return new PaymentsConfigDto { Enabled = opts.Enabled, MerchantConfigured = opts.MerchantConfigured };
    }

    /// <summary>Errors: PAYMENTS_OFF, BAD_PLAN, BAD_PERIOD, NO_PRICE, HIGHER_ACTIVE, TOO_MANY.</summary>
    [HttpPost("orders")]
    [RequireAccessToken]
    public async Task<ActionResult<CreatePaymentOrderResponse>> CreateOrder(CreatePaymentOrderRequest request)
    {
        var opts = PaymeOptions.From(config);
        if (!opts.Enabled || !opts.MerchantConfigured) return Conflict(new { error = "PAYMENTS_OFF" });

        var plan = Plans.Normalize(request.Plan);
        if (plan == Plans.Bronze || !string.Equals(request.Plan, plan, StringComparison.OrdinalIgnoreCase)) return BadRequest(new { error = "BAD_PLAN" });
        var period = request.Period?.Trim().ToLowerInvariant();
        if (period is null || !Plans.Periods.Contains(period)) return BadRequest(new { error = "BAD_PERIOD" });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var price = await db.PlanPrices.AsNoTracking().SingleOrDefaultAsync(p => p.Plan == plan && p.Period == period);
        if (price is null || price.Amount <= 0) return Conflict(new { error = "NO_PRICE" });

        var now = clock.GetUtcNow().UtcDateTime;
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => new { u.Plan, u.PlanUntil }).SingleAsync();
        if (Plans.Rank(Plans.Effective(user.Plan, user.PlanUntil, now)) > Plans.Rank(plan)) return Conflict(new { error = "HIGHER_ACTIVE" });

        // a click that comes again (or a reloaded page) uses the order that nobody has started to pay yet
        var recent = now.AddHours(-1);
        var order = await db.PaymentOrders.Where(o => o.UserId == userId && o.Plan == plan && o.Period == period && o.AmountSum == price.Amount
                                                      && o.Status == PaymentOrderStatus.New && o.CreatedAt > recent)
            .OrderByDescending(o => o.PaymentOrderId).FirstOrDefaultAsync();
        if (order is null)
        {
            if (await db.PaymentOrders.CountAsync(o => o.UserId == userId && o.CreatedAt > now.AddDays(-1)) >= MaxOrdersPerDay)
                return StatusCode(StatusCodes.Status429TooManyRequests, new { error = "TOO_MANY" });
            order = new PaymentOrder { UserId = userId, Plan = plan, Period = period, AmountSum = price.Amount, CreatedAt = now };
            db.PaymentOrders.Add(order);
            await db.SaveChangesAsync();
        }

        return new CreatePaymentOrderResponse
        {
            OrderId = order.PaymentOrderId, AmountSum = order.AmountSum,
            CheckoutUrl = opts.CheckoutLink(order.PaymentOrderId, order.AmountSum * 100, PaymeOptions.PageLang(request.Lang)),
        };
    }

    /// <summary>The state of the caller's own order (the page the learner returns to asks it until it is paid). Another learner's order is 404.</summary>
    [HttpGet("orders/{id:long}")]
    [RequireAccessToken]
    public async Task<ActionResult<PaymentOrderDto>> GetOrder(long id)
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        Response.Headers.CacheControl = "no-store";
        var order = await db.PaymentOrders.AsNoTracking().Where(o => o.PaymentOrderId == id && o.UserId == userId)
            .Select(o => new PaymentOrderDto
            {
                OrderId = o.PaymentOrderId, Plan = o.Plan, Period = o.Period, AmountSum = o.AmountSum, Status = o.Status, CreatedAt = o.CreatedAt, PaidAt = o.PaidAt
            }).SingleOrDefaultAsync();
        return order is null ? NotFound() : order;
    }
}
