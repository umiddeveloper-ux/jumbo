using Jumbo.Api.Data;
using Jumbo.Shared;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Jumbo.Api.Services.Payme;

/// <summary>
/// The Payme Merchant API (variant A: one order per paid version). Each method answers with the object of its "result" or throws a
/// <see cref="PaymeException"/>. Money paths run in a database transaction, under a lock of the order, so a repeated or a parallel call of Payme
/// can never switch a version on twice; the unique indexes (the Payme id, one live transaction per order) are the last guard.
/// Amounts are in tiyin on the wire (so'm x 100), times in Unix milliseconds.
/// </summary>
public class PaymeService(AppDbContext db, IConfiguration config, TimeProvider clock, ILogger<PaymeService> log)
{
    /// <summary>An unfinished transaction is cancelled after 12 hours (reason 4).</summary>
    public const long TimeoutMs = 12L * 60 * 60 * 1000;

    public const int ReasonTimeout = 4;

    private static bool _fiscalWarned;

    private long NowMs => clock.GetUtcNow().ToUnixTimeMilliseconds();
    private DateTime NowUtc => clock.GetUtcNow().UtcDateTime;

    // ---- the methods -------------------------------------------------------------------------------------------------------------------

    public async Task<object> CheckPerformAsync(long? orderId, long amountTiyin)
    {
        var order = await FindOrderAsync(orderId);
        await ValidateForPaymentAsync(order, amountTiyin, creating: false);

        var opts = PaymeOptions.From(config);
        if (!opts.FiscalConfigured)
        {
            if (!_fiscalWarned) { _fiscalWarned = true; log.LogWarning("Payme: Payme:Mxik / PackageCode / VatPercent are not set, the receipt data is left out of CheckPerformTransaction"); }
            return new { allow = true };
        }
        return new
        {
            allow = true,
            detail = new
            {
                receipt_type = 0,
                items = new[]
                {
                    new
                    {
                        title = ItemTitle(order!.Plan, order.Period),
                        price = amountTiyin,
                        count = 1,
                        code = opts.Mxik,
                        package_code = opts.PackageCode,
                        vat_percent = opts.VatPercent!.Value,
                    }
                }
            }
        };
    }

    public async Task<object> CreateAsync(string paymeId, long timeMs, long amountTiyin, long? orderId)
    {
        try
        {
            return await CreateCoreAsync(paymeId, timeMs, amountTiyin, orderId);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // two calls met: the loser of the unique index re-reads what the winner wrote
            db.ChangeTracker.Clear();
            var t = await db.PaymeTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.PaymeId == paymeId);
            if (t is { State: PaymeStates.Created }) return CreateAnswer(t);
            throw PaymeException.CannotPerform();
        }
    }

    private async Task<object> CreateCoreAsync(string paymeId, long timeMs, long amountTiyin, long? orderId)
    {
        if (orderId is null) throw PaymeException.OrderNotFound();
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockAsync(orderId.Value);

        // a repeated call (Payme sends it again when the answer was lost) gets the same answer
        var existing = await db.PaymeTransactions.SingleOrDefaultAsync(t => t.PaymeId == paymeId);
        if (existing is not null)
        {
            if (existing.State == PaymeStates.Created) return CreateAnswer(existing);
            throw PaymeException.CannotPerform();
        }

        var order = await FindOrderAsync(orderId);
        await ValidateForPaymentAsync(order, amountTiyin, creating: true);

        var row = new PaymeTransaction
        {
            PaymeId = paymeId, TimeMs = timeMs, AmountTiyin = amountTiyin, PaymentOrderId = order!.PaymentOrderId,
            State = PaymeStates.Created, CreateTimeMs = NowMs,
        };
        db.PaymeTransactions.Add(row);
        order.Status = PaymentOrderStatus.Waiting;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CreateAnswer(row);
    }

    public async Task<object> PerformAsync(string paymeId)
    {
        var orderId = await OrderIdOfAsync(paymeId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockAsync(orderId);

        var t = await db.PaymeTransactions.SingleOrDefaultAsync(x => x.PaymeId == paymeId) ?? throw PaymeException.TransactionNotFound();
        if (t.State == PaymeStates.Completed) return PerformAnswer(t);      // a repeat
        if (t.State != PaymeStates.Created) throw PaymeException.CannotPerform();

        var order = await db.PaymentOrders.SingleAsync(o => o.PaymentOrderId == orderId);

        // too late: the transaction timed out (the background job may not have reached it yet)
        if (NowMs - t.CreateTimeMs > TimeoutMs)
        {
            Cancel(t, order, PaymeStates.Cancelled, ReasonTimeout);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            throw PaymeException.CannotPerform();
        }

        // the account is gone (the learner deleted it): there is nobody to give the version to
        if (order.UserId is not { } userId) throw PaymeException.CannotPerform();
        await LockUserAsync(userId);
        var user = await db.Users.SingleAsync(u => u.UserId == userId);

        // the same rules as buying from the balance (PlansController.Purchase)
        var now = NowUtc;
        var inForce = Plans.Effective(user.Plan, user.PlanUntil, now);
        if (Plans.Rank(inForce) > Plans.Rank(order.Plan)) throw PaymeException.CannotPerform();
        var from = Plans.PeriodStart(inForce, order.Plan, user.PlanUntil, now);
        var until = DateTime.SpecifyKind(Plans.AddPeriod(from, order.Period), DateTimeKind.Utc);

        order.PlanBefore = user.Plan;
        order.PlanUntilBefore = user.PlanUntil;
        order.PlanUntilAfter = until;
        order.PaidAt = now;
        order.Status = PaymentOrderStatus.Paid;
        user.Plan = order.Plan;
        user.PlanUntil = until;

        t.State = PaymeStates.Completed;
        t.PerformTimeMs = NowMs;
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return PerformAnswer(t);
    }

    public async Task<object> CancelAsync(string paymeId, int reason)
    {
        var orderId = await OrderIdOfAsync(paymeId);
        await using var tx = await db.Database.BeginTransactionAsync();
        await LockAsync(orderId);

        var t = await db.PaymeTransactions.SingleOrDefaultAsync(x => x.PaymeId == paymeId) ?? throw PaymeException.TransactionNotFound();
        if (t.State < 0) return CancelAnswer(t);                         // a repeat

        var order = await db.PaymentOrders.SingleAsync(o => o.PaymentOrderId == orderId);
        if (t.State == PaymeStates.Created)
        {
            Cancel(t, order, PaymeStates.Cancelled, reason);
        }
        else
        {
            // a refund: the period that the payment gave is taken back
            if (order.UserId is { } userId)
            {
                await LockUserAsync(userId);
                var user = await db.Users.SingleAsync(u => u.UserId == userId);
                RevokePeriod(user, order);
            }
            Cancel(t, order, PaymeStates.CancelledAfterCompletion, reason);
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return CancelAnswer(t);
    }

    public async Task<object> CheckAsync(string paymeId)
    {
        var t = await db.PaymeTransactions.AsNoTracking().SingleOrDefaultAsync(x => x.PaymeId == paymeId) ?? throw PaymeException.TransactionNotFound();
        return new
        {
            create_time = t.CreateTimeMs, perform_time = t.PerformTimeMs, cancel_time = t.CancelTimeMs,
            transaction = t.PaymeTransactionId.ToString(), state = t.State, reason = t.Reason,
        };
    }

    public async Task<object> StatementAsync(long from, long to)
    {
        var rows = await db.PaymeTransactions.AsNoTracking()
            .Where(t => t.TimeMs >= from && t.TimeMs <= to)
            .OrderBy(t => t.TimeMs).ThenBy(t => t.PaymeTransactionId)
            .ToListAsync();
        return new
        {
            transactions = rows.Select(t => new
            {
                id = t.PaymeId, time = t.TimeMs, amount = t.AmountTiyin,
                account = new { order_id = t.PaymentOrderId.ToString() },
                create_time = t.CreateTimeMs, perform_time = t.PerformTimeMs, cancel_time = t.CancelTimeMs,
                transaction = t.PaymeTransactionId.ToString(), state = t.State, reason = t.Reason,
                receivers = (object?)null,
            }).ToList()
        };
    }

    /// <summary>Cancels the transactions that were created more than 12 hours ago and never paid (reason 4). Called every minute.</summary>
    public async Task<int> ExpireAsync()
    {
        var cutoff = NowMs - TimeoutMs;
        var due = await db.PaymeTransactions.AsNoTracking()
            .Where(t => t.State == PaymeStates.Created && t.CreateTimeMs < cutoff)
            .Select(t => new { t.PaymeId, t.PaymentOrderId }).Take(200).ToListAsync();
        var done = 0;
        foreach (var d in due)
        {
            db.ChangeTracker.Clear();
            await using var tx = await db.Database.BeginTransactionAsync();
            await LockAsync(d.PaymentOrderId);
            var t = await db.PaymeTransactions.SingleOrDefaultAsync(x => x.PaymeId == d.PaymeId);
            if (t is not { State: PaymeStates.Created } || t.CreateTimeMs >= cutoff) continue;      // paid or cancelled meanwhile
            var order = await db.PaymentOrders.SingleAsync(o => o.PaymentOrderId == d.PaymentOrderId);
            Cancel(t, order, PaymeStates.Cancelled, ReasonTimeout);
            await db.SaveChangesAsync();
            await tx.CommitAsync();
            done++;
        }
        return done;
    }

    // ---- the rules ---------------------------------------------------------------------------------------------------------------------

    private async Task<PaymentOrder?> FindOrderAsync(long? orderId) =>
        orderId is null ? null : await db.PaymentOrders.SingleOrDefaultAsync(o => o.PaymentOrderId == orderId);

    private async Task ValidateForPaymentAsync(PaymentOrder? order, long amountTiyin, bool creating)
    {
        if (order is null) throw PaymeException.OrderNotFound();
        if (amountTiyin != order.AmountSum * 100) throw PaymeException.WrongAmount();
        if (order.Status is PaymentOrderStatus.Paid or PaymentOrderStatus.Cancelled) throw PaymeException.OrderNotPayable();

        // a payment of the order is on its way already: a second one is not created
        var live = await db.PaymeTransactions.AnyAsync(t => t.PaymentOrderId == order.PaymentOrderId && (t.State == PaymeStates.Created || t.State == PaymeStates.Completed));
        if (live) throw creating ? PaymeException.CannotPerform() : PaymeException.OrderWaiting();

        // the version must still be possible to buy (a higher one may have been bought meanwhile)
        if (order.UserId is not { } userId) throw PaymeException.OrderNotPayable();
        var user = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => new { u.Plan, u.PlanUntil }).SingleOrDefaultAsync();
        if (user is null) throw PaymeException.OrderNotPayable();
        if (Plans.Rank(Plans.Effective(user.Plan, user.PlanUntil, NowUtc)) > Plans.Rank(order.Plan)) throw PaymeException.PlanNotAvailable();
    }

    private async Task<long> OrderIdOfAsync(string paymeId) =>
        await db.PaymeTransactions.AsNoTracking().Where(t => t.PaymeId == paymeId).Select(t => (long?)t.PaymentOrderId).SingleOrDefaultAsync()
        ?? throw PaymeException.TransactionNotFound();

    private void Cancel(PaymeTransaction t, PaymentOrder order, int state, int reason)
    {
        t.State = state;
        t.Reason = reason;
        t.CancelTimeMs = NowMs;
        // an unpaid order may be paid again; a refunded one is closed
        order.Status = state == PaymeStates.CancelledAfterCompletion ? PaymentOrderStatus.Cancelled : PaymentOrderStatus.New;
    }

    /// <summary>
    /// Takes back what the payment gave. When the version still ends where the payment made it end, the learner gets exactly what he had before;
    /// when it was lengthened afterwards, one period is taken off the end.
    /// </summary>
    private void RevokePeriod(User user, PaymentOrder order)
    {
        if (user.PlanUntil is not { } until) return;
        if (order.PlanUntilAfter is { } after && until == after)
        {
            user.Plan = order.PlanBefore ?? Plans.Bronze;
            user.PlanUntil = order.PlanUntilBefore;
            return;
        }
        var back = Plans.SubtractPeriod(until, order.Period);
        user.PlanUntil = back;
        if (back <= NowUtc) user.Plan = Plans.Bronze;
    }

    private static object CreateAnswer(PaymeTransaction t) => new { create_time = t.CreateTimeMs, transaction = t.PaymeTransactionId.ToString(), state = t.State };
    private static object PerformAnswer(PaymeTransaction t) => new { transaction = t.PaymeTransactionId.ToString(), perform_time = t.PerformTimeMs, state = t.State };
    private static object CancelAnswer(PaymeTransaction t) => new { transaction = t.PaymeTransactionId.ToString(), cancel_time = t.CancelTimeMs, state = t.State };

    /// <summary>The name of the sale on the receipt (Uzbek: the receipt is read by the buyer and the tax office here).</summary>
    public static string ItemTitle(string plan, string period) =>
        $"Jumbo {(plan == Plans.Gold ? "Gold" : "Silver")} — {(period == Plans.Year ? "1 yillik" : "1 oylik")} obuna";

    // ---- locks and errors --------------------------------------------------------------------------------------------------------------

    // One lock per order (and per learner for the version switch), held to the end of the transaction. Always the order first, then the
    // learner, so two calls cannot wait for each other. Negative keys are the learners, positive the orders.
    private Task LockAsync(long orderId) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({orderId})");
    private Task LockUserAsync(int userId) => db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({-(long)userId})");

    private static bool IsUniqueViolation(DbUpdateException ex) => ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
