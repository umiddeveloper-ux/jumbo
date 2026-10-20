using Jumbo.Api.Data;
using Jumbo.Shared.Entities;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Api.Services;

/// <summary>
/// The learner's balance (so'm). Putting money on it is one place: the admin's manual top-up uses it now, and the payment services
/// (Payme, Click, Paynet) will call it from their webhooks with their own payment id, which is never counted twice.
/// </summary>
public static class BalanceService
{
    public static readonly string[] Providers = ["payme", "click", "paynet", "admin"];

    public enum TopUpResult { Done, AlreadyDone, NoUser }

    public static async Task<(TopUpResult Result, long Balance)> TopUpAsync(
        AppDbContext db, int userId, long amount, string provider, string? externalId, string? note)
    {
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Providers.Contains(provider)) throw new ArgumentException("Unknown provider.", nameof(provider));
        externalId = string.IsNullOrWhiteSpace(externalId) ? null : externalId.Trim();

        await using var tx = await db.Database.BeginTransactionAsync();

        // The same payment of a service arrives twice (it repeats its call when the answer is late): the second time nothing is added.
        if (externalId is not null &&
            await db.BalanceTransactions.AnyAsync(t => t.Provider == provider && t.ExternalId == externalId))
        {
            var current = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => (long?)u.Balance).SingleOrDefaultAsync();
            return (TopUpResult.AlreadyDone, current ?? 0);
        }

        var rows = await db.Users.Where(u => u.UserId == userId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Balance, u => u.Balance + amount));
        if (rows == 0) return (TopUpResult.NoUser, 0);

        var balance = await db.Users.AsNoTracking().Where(u => u.UserId == userId).Select(u => u.Balance).SingleAsync();
        db.BalanceTransactions.Add(new BalanceTransaction
        {
            UserId = userId, Amount = amount, BalanceAfter = balance, Kind = BalanceKinds.TopUp,
            Provider = provider, ExternalId = externalId, Note = Clip(note, 200)
        });
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return (TopUpResult.Done, balance);
    }

    private static string? Clip(string? v, int max)
    {
        var t = v?.Trim();
        if (string.IsNullOrEmpty(t)) return null;
        return t.Length > max ? t[..max] : t;
    }
}
