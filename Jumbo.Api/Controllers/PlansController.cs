using Jumbo.Api.Auth;
using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Controllers;

/// <summary>The versions of the app (Bronze / Silver / Gold): the prices, and buying a version from the learner's balance.</summary>
[ApiController]
[Route("api/plans")]
public class PlansController(AppDbContext db, ICurrentUserService currentUser) : ControllerBase
{
    /// <summary>The four prices (Silver and Gold, a month and a year). Open to everybody: the page of the versions is for guests too.</summary>
    [HttpGet("prices")]
    public async Task<List<PlanPriceDto>> Prices()
    {
        Response.Headers.CacheControl = "no-store";
        return await AllPricesAsync(db);
    }

    public static async Task<List<PlanPriceDto>> AllPricesAsync(AppDbContext db)
    {
        var rows = await db.PlanPrices.AsNoTracking().ToListAsync();
        return PaidPlans.SelectMany(plan => Plans.Periods.Select(period => new PlanPriceDto
        {
            Plan = plan, Period = period,
            Amount = rows.FirstOrDefault(r => r.Plan == plan && r.Period == period) is { Amount: > 0 } r ? r.Amount : null
        })).ToList();
    }

    public static readonly string[] PaidPlans = [Plans.Silver, Plans.Gold];

    /// <summary>The caller's last movements of the balance.</summary>
    [HttpGet("transactions")]
    [RequireAccessToken]
    public async Task<List<BalanceTransactionDto>> Transactions()
    {
        var userId = (await currentUser.GetUserIdAsync())!.Value;
        return await db.BalanceTransactions.AsNoTracking().Where(t => t.UserId == userId)
            .OrderByDescending(t => t.BalanceTransactionId).Take(20)
            .Select(t => new BalanceTransactionDto
            {
                CreatedAt = t.CreatedAt, Kind = t.Kind, Provider = t.Provider, Amount = t.Amount, BalanceAfter = t.BalanceAfter, Note = t.Note
            }).ToListAsync();
    }

    /// <summary>
    /// Buys a version for a period from the balance. The same version again lengthens it (from the day it ends); another one starts now and
    /// replaces the one that is in force. A lower version cannot be bought while a higher one is in force.
    /// Errors: BAD_PLAN, BAD_PERIOD, NO_PRICE, HIGHER_ACTIVE, NOT_ENOUGH.
    /// </summary>
    [HttpPost("purchase")]
    [RequireAccessToken]
    public async Task<ActionResult<PurchasePlanResponse>> Purchase(PurchasePlanRequest request)
    {
        var plan = Plans.Normalize(request.Plan);
        if (plan == Plans.Bronze || !string.Equals(request.Plan, plan, StringComparison.OrdinalIgnoreCase))
            return BadRequest(new { error = "BAD_PLAN" });
        var period = request.Period?.Trim().ToLowerInvariant();
        if (period is null || !Plans.Periods.Contains(period)) return BadRequest(new { error = "BAD_PERIOD" });

        var userId = (await currentUser.GetUserIdAsync())!.Value;
        var price = await db.PlanPrices.AsNoTracking().SingleOrDefaultAsync(p => p.Plan == plan && p.Period == period);
        if (price is null || price.Amount <= 0) return Conflict(new { error = "NO_PRICE" });

        await using var tx = await db.Database.BeginTransactionAsync();
        var user = await db.Users.AsNoTracking().SingleAsync(u => u.UserId == userId);
        var now = DateTime.UtcNow;
        var inForce = Plans.Effective(user.Plan, user.PlanUntil, now);

        if (Plans.Rank(inForce) > Plans.Rank(plan)) return Conflict(new { error = "HIGHER_ACTIVE" });
        var from = inForce == plan ? user.PlanUntil!.Value : now;
        var until = DateTime.SpecifyKind(Plans.AddPeriod(from, period), DateTimeKind.Utc);

        // the money is taken only if it is there (no balance below zero, even when two purchases arrive together)
        var rows = await db.Users.Where(u => u.UserId == userId && u.Balance >= price.Amount)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Balance, u => u.Balance - price.Amount)
                .SetProperty(u => u.Plan, plan)
                .SetProperty(u => u.PlanUntil, (DateTime?)until));
        if (rows == 0) return Conflict(new { error = "NOT_ENOUGH" });

        var balance = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Balance).SingleAsync();
        db.BalanceTransactions.Add(new BalanceTransaction
        {
            UserId = userId, Amount = -price.Amount, BalanceAfter = balance, Kind = BalanceKinds.Purchase, Note = $"{plan}/{period}"
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return new PurchasePlanResponse { Plan = plan, PlanUntil = until, Balance = balance };
    }
}
