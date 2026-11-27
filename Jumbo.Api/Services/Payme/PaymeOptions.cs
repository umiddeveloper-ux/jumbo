using System.Net;
using System.Text;

namespace Jumbo.Api.Services.Payme;

/// <summary>
/// The settings of the Payme payment (environment variables Payme__Login, Payme__Key, ... on the server; never in the repository).
/// Without Login and Key the Merchant API answers "no rights" (-32504) to everybody and the site hides the button.
/// </summary>
public class PaymeOptions
{
    /// <summary>The login of the Basic authorization Payme calls us with (given by Payme).</summary>
    public string Login { get; init; } = string.Empty;

    /// <summary>The key (password) of the cash register: the test key for the sandbox, the real key for production.</summary>
    public string Key { get; init; } = string.Empty;

    /// <summary>The id of the cash register (web cashier); goes in the checkout link as <c>m</c>.</summary>
    public string MerchantId { get; init; } = string.Empty;

    /// <summary>https://checkout.paycom.uz (production) or https://test.paycom.uz (sandbox).</summary>
    public string CheckoutUrl { get; init; } = "https://checkout.paycom.uz";

    /// <summary>The site the learner returns to after the payment page.</summary>
    public string ReturnBaseUrl { get; init; } = "https://jumbo.uz";

    // The fiscal receipt (the tax office sees the sale through these): the product code (MXIK / IKPU), the unit of measure of that code
    // and the VAT percent. Without all three the receipt data is left out of CheckPerformTransaction.
    public string Mxik { get; init; } = string.Empty;
    public string PackageCode { get; init; } = string.Empty;
    public int? VatPercent { get; init; }

    /// <summary>Only the addresses of Payme may call the Merchant API. Null = on in production, off elsewhere.</summary>
    public bool? CheckIp { get; init; }

    /// <summary>Comma separated: addresses or ranges "a-b". Default: Payme's 185.234.113.1 - 185.234.113.15.</summary>
    public string AllowedIps { get; init; } = "185.234.113.1-185.234.113.15";

    /// <summary>The Merchant API is on (the login and the key are set).</summary>
    public bool Enabled => Login.Length > 0 && Key.Length > 0;

    public bool MerchantConfigured => MerchantId.Length > 0;

    public bool FiscalConfigured => Mxik.Length > 0 && PackageCode.Length > 0 && VatPercent is not null;

    public static PaymeOptions From(IConfiguration c)
    {
        string S(string key) => (c["Payme:" + key] ?? string.Empty).Trim();
        var checkout = S("CheckoutUrl");
        var back = S("ReturnBaseUrl");
        var allowed = S("AllowedIps");
        return new PaymeOptions
        {
            Login = S("Login"),
            Key = S("Key"),
            MerchantId = S("MerchantId"),
            CheckoutUrl = checkout.Length > 0 ? checkout.TrimEnd('/') : "https://checkout.paycom.uz",
            ReturnBaseUrl = back.Length > 0 ? back.TrimEnd('/') : "https://jumbo.uz",
            Mxik = S("Mxik"),
            PackageCode = S("PackageCode"),
            VatPercent = int.TryParse(S("VatPercent"), out var vat) ? vat : null,
            CheckIp = bool.TryParse(S("CheckIp"), out var ip) ? ip : null,
            AllowedIps = allowed.Length > 0 ? allowed : "185.234.113.1-185.234.113.15",
        };
    }

    /// <summary>Is this address one of the allowed ones (addresses and ranges "a-b" of the list)?</summary>
    public bool IsAllowed(IPAddress? address)
    {
        if (address is null) return false;
        if (address.IsIPv4MappedToIPv6) address = address.MapToIPv4();
        foreach (var part in AllowedIps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var ends = part.Split('-', 2, StringSplitOptions.TrimEntries);
            if (!IPAddress.TryParse(ends[0], out var from)) continue;
            var to = ends.Length == 2 && IPAddress.TryParse(ends[1], out var t) ? t : from;
            if (InRange(address, from, to)) return true;
        }
        return false;
    }

    private static bool InRange(IPAddress a, IPAddress from, IPAddress to)
    {
        var x = a.GetAddressBytes();
        var lo = from.GetAddressBytes();
        var hi = to.GetAddressBytes();
        if (x.Length != lo.Length || x.Length != hi.Length) return false;
        return x.AsSpan().SequenceCompareTo(lo) >= 0 && x.AsSpan().SequenceCompareTo(hi) <= 0;
    }

    /// <summary>
    /// The Payme page of an order: <c>{CheckoutUrl}/base64("m=...;ac.order_id=...;a=tiyin;l=lang;c=return url;ct=15000")</c>. The base64 text must not
    /// hold a "/" or a "+" (a path segment): the wait time after the payment is moved by a few milliseconds until it does not.
    /// </summary>
    public string CheckoutLink(long orderId, long amountTiyin, string lang)
    {
        for (var ct = 15000; ct < 15100; ct++)
        {
            var text = $"m={MerchantId};ac.order_id={orderId};a={amountTiyin};l={lang};c={ReturnBaseUrl}/plans/paid/{orderId};ct={ct}";
            var b64 = Convert.ToBase64String(Encoding.UTF8.GetBytes(text));
            if (b64.IndexOfAny(['/', '+']) < 0) return $"{CheckoutUrl}/{b64}";
        }
        return $"{CheckoutUrl}/{Convert.ToBase64String(Encoding.UTF8.GetBytes($"m={MerchantId};ac.order_id={orderId};a={amountTiyin};l={lang};c={ReturnBaseUrl}/plans/paid/{orderId}"))}";
    }

    /// <summary>The language of the Payme page: ru | uz | en.</summary>
    public static string PageLang(string? lang) => lang?.Trim().ToLowerInvariant() switch { "ru" => "ru", "uz" => "uz", _ => "en" };
}

/// <summary>An error of the Merchant API, answered to Payme as a JSON-RPC error (with HTTP 200, as the protocol wants).</summary>
public class PaymeException(int code, string ru, string uz, string en, string? data = null) : Exception($"{code}: {en}")
{
    public int Code { get; } = code;
    public string Ru { get; } = ru;
    public string Uz { get; } = uz;
    public string En { get; } = en;
    public string? Data { get; } = data;

    // the errors of the protocol
    public static PaymeException NoRights() => new(-32504, "Недостаточно привилегий для выполнения метода", "Usulni bajarish uchun huquqlar yetarli emas", "Insufficient privileges to perform the method");
    public static PaymeException NotPost() => new(-32300, "Метод запроса должен быть POST", "So'rov usuli POST bo'lishi kerak", "The request method must be POST");
    public static PaymeException ParseError() => new(-32700, "Ошибка разбора JSON", "JSON o'qishda xato", "JSON parse error");
    public static PaymeException InvalidRequest(string? data = null) => new(-32600, "Неверный запрос", "Noto'g'ri so'rov", "Invalid request", data);
    public static PaymeException NoMethod(string name) => new(-32601, "Метод не найден", "Usul topilmadi", "Method not found", name);
    public static PaymeException Internal() => new(-32400, "Внутренняя ошибка сервера", "Serverning ichki xatosi", "Internal server error");

    // the errors of the methods
    public static PaymeException WrongAmount() => new(-31001, "Неверная сумма", "Noto'g'ri summa", "Wrong amount");
    public static PaymeException TransactionNotFound() => new(-31003, "Транзакция не найдена", "Tranzaksiya topilmadi", "Transaction not found");
    public static PaymeException CannotPerform() => new(-31008, "Невозможно выполнить операцию", "Operatsiyani bajarib bo'lmaydi", "Unable to perform the operation");

    // a wrong account (the order): the range -31050 ... -31099, data = the name of the account field
    public static PaymeException OrderNotFound() => new(-31050, "Заказ не найден", "Buyurtma topilmadi", "Order not found", "order_id");
    public static PaymeException OrderNotPayable() => new(-31051, "Заказ уже оплачен или отменён", "Buyurtma allaqachon to'langan yoki bekor qilingan", "The order is already paid or cancelled", "order_id");
    public static PaymeException PlanNotAvailable() => new(-31052, "Эту версию сейчас нельзя купить", "Bu tarifni hozir sotib olib bo'lmaydi", "This version cannot be bought now", "order_id");
    public static PaymeException OrderWaiting() => new(-31053, "Заказ ожидает оплаты", "Buyurtma to'lovni kutmoqda", "The order is waiting for payment", "order_id");
}
