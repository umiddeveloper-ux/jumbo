namespace Jumbo.Shared.Dtos;

/// <summary>What the site needs to know to show the "Pay with Payme" button.</summary>
public class PaymentsConfigDto
{
    /// <summary>The Merchant API is switched on (login and key are set); without it Payme gets -32504.</summary>
    public bool Enabled { get; set; }

    /// <summary>The cash register id is set, so a checkout link can be made. The button needs both.</summary>
    public bool MerchantConfigured { get; set; }
}

public class CreatePaymentOrderRequest
{
    public string Plan { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;

    /// <summary>The language of the Payme page: ru | uz | en (anything else becomes en).</summary>
    public string? Lang { get; set; }
}

public class CreatePaymentOrderResponse
{
    public long OrderId { get; set; }
    public long AmountSum { get; set; }

    /// <summary>The Payme page to send the learner to.</summary>
    public string CheckoutUrl { get; set; } = string.Empty;
}

public class PaymentOrderDto
{
    public long OrderId { get; set; }
    public string Plan { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public long AmountSum { get; set; }

    /// <summary>new | waiting | paid | cancelled (see Jumbo.Shared.Entities.PaymentOrderStatus).</summary>
    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }
}

/// <summary>Admin, read-only: an order with the state of its Payme transaction.</summary>
public class AdminPaymentDto
{
    public long OrderId { get; set; }
    public int? UserId { get; set; }
    public string? UserName { get; set; }
    public string Plan { get; set; } = string.Empty;
    public string Period { get; set; } = string.Empty;
    public long AmountSum { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PaidAt { get; set; }

    /// <summary>The newest Payme transaction of the order, if any.</summary>
    public string? PaymeId { get; set; }
    public int? PaymeState { get; set; }
    public int? PaymeReason { get; set; }
}
