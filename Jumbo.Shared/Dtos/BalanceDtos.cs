namespace Jumbo.Shared.Dtos;

/// <summary>The price of a version for a period; Amount is null when the admin has not set it yet.</summary>
public class PlanPriceDto
{
    public string Plan { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public long? Amount { get; set; }
}

public class SetPlanPricesRequest
{
    public List<PlanPriceDto> Prices { get; set; } = new();
}

public class PurchasePlanRequest
{
    public string Plan { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
}

public class PurchasePlanResponse
{
    public string Plan { get; set; } = string.Empty;
    public DateTime PlanUntil { get; set; }
    public long Balance { get; set; }
}

/// <summary>Admin: put money on a learner's balance by hand (the payment services will do it through the same code later).</summary>
public class TopUpRequest
{
    /// <summary>The login or the ID (A0000001) of the learner.</summary>
    public string Username { get; set; } = string.Empty;
    public long Amount { get; set; }
    public string? Note { get; set; }
}

public class BalanceTransactionDto
{
    public DateTime CreatedAt { get; set; }
    public string Kind { get; set; } = string.Empty;
    public string? Provider { get; set; }
    public long Amount { get; set; }
    public long BalanceAfter { get; set; }
    public string? Note { get; set; }

    // Admin list only.
    public string? PublicId { get; set; }
    public string? UserName { get; set; }
}
