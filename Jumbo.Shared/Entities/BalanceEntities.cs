namespace Jumbo.Shared.Entities;

/// <summary>
/// One movement of a learner's balance (so'm): a top-up (+) made through a payment service or by the admin, or the purchase of a version (-).
/// The balance itself is User.Balance; this is its journal, so every number can be explained and a payment service's call is never counted twice.
/// </summary>
public class BalanceTransaction
{
    public long BalanceTransactionId { get; set; }

    public int UserId { get; set; }

    /// <summary>Positive for a top-up, negative for a purchase.</summary>
    public long Amount { get; set; }

    /// <summary>The balance after this movement.</summary>
    public long BalanceAfter { get; set; }

    /// <summary>"topup" or "purchase".</summary>
    public string Kind { get; set; } = BalanceKinds.TopUp;

    /// <summary>Who brought the money: "payme", "click", "paynet", "admin"; null for a purchase.</summary>
    public string? Provider { get; set; }

    /// <summary>The id of the payment on the side of the service (a repeated call of the service with the same id is not counted again).</summary>
    public string? ExternalId { get; set; }

    /// <summary>For a purchase: "silver/year"; for a top-up: a note.</summary>
    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public static class BalanceKinds
{
    public const string TopUp = "topup";
    public const string Purchase = "purchase";
}

/// <summary>The price (so'm) of a paid version for a period; a version with no row or a zero price cannot be bought yet. The admin sets them.</summary>
public class PlanPrice
{
    /// <summary>"silver" or "gold".</summary>
    public string Plan { get; set; } = null!;

    /// <summary>"month" or "year".</summary>
    public string Period { get; set; } = null!;

    public long Amount { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
