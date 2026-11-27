namespace Jumbo.Shared.Entities;

/// <summary>
/// One order for a paid version (variant A of the Payme payment): the learner picks "Silver, a month", the site creates an order, and Payme is
/// asked to charge exactly that order. The order number is the account (<c>order_id</c>) Payme sends to our Merchant API. The amount is taken
/// from the plan prices when the order is made (never from the client). When Payme confirms the payment the version is switched on; when it
/// is refunded the period is taken back.
/// </summary>
public class PaymentOrder
{
    public long PaymentOrderId { get; set; }

    /// <summary>The learner. Null after the account was deleted: the order and its Payme transactions stay for the accounts of the business.</summary>
    public int? UserId { get; set; }

    /// <summary>"silver" or "gold".</summary>
    public string Plan { get; set; } = null!;

    /// <summary>"month" or "year".</summary>
    public string Period { get; set; } = null!;

    /// <summary>The price in so'm at the moment of the order (Payme gets it in tiyin: x 100).</summary>
    public long AmountSum { get; set; }

    /// <summary>See <see cref="PaymentOrderStatus"/>.</summary>
    public string Status { get; set; } = PaymentOrderStatus.New;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? PaidAt { get; set; }

    // What the learner had before the payment switched the version on, and what the payment made of it: a refund puts it back.
    public string? PlanBefore { get; set; }
    public DateTime? PlanUntilBefore { get; set; }
    public DateTime? PlanUntilAfter { get; set; }
}

public static class PaymentOrderStatus
{
    /// <summary>Made, nobody has started to pay it (or the payment that was started timed out / was cancelled).</summary>
    public const string New = "new";

    /// <summary>Payme created a transaction for it and waits for the card (at most 12 hours).</summary>
    public const string Waiting = "waiting";

    /// <summary>Paid: the version is switched on.</summary>
    public const string Paid = "paid";

    /// <summary>Paid and then refunded: the period was taken back; the order cannot be paid again.</summary>
    public const string Cancelled = "cancelled";
}

/// <summary>
/// A transaction of the Payme Merchant API (the record Payme asks about with CheckTransaction / GetStatement). Times are Unix milliseconds,
/// 0 = not set. An order has at most one live transaction (state 1 or 2).
/// </summary>
public class PaymeTransaction
{
    /// <summary>Our id of the transaction (the <c>transaction</c> field of the answers).</summary>
    public long PaymeTransactionId { get; set; }

    /// <summary>The id Payme gave it (24 characters); unique, so a repeated call finds the same row.</summary>
    public string PaymeId { get; set; } = null!;

    /// <summary>The time Payme sent with CreateTransaction (the statement is searched by it).</summary>
    public long TimeMs { get; set; }

    /// <summary>The amount in tiyin.</summary>
    public long AmountTiyin { get; set; }

    public long PaymentOrderId { get; set; }

    /// <summary>1 created, 2 completed, -1 cancelled before completion, -2 cancelled after completion.</summary>
    public int State { get; set; } = PaymeStates.Created;

    /// <summary>Why it was cancelled (Payme's reason code), null while it is not.</summary>
    public int? Reason { get; set; }

    public long CreateTimeMs { get; set; }
    public long PerformTimeMs { get; set; }
    public long CancelTimeMs { get; set; }
}

public static class PaymeStates
{
    public const int Created = 1;
    public const int Completed = 2;
    public const int Cancelled = -1;
    public const int CancelledAfterCompletion = -2;
}
