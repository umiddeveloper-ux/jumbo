using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using Jumbo.Api.Services.Payme;
using Microsoft.AspNetCore.Mvc;

namespace Jumbo.Api.Controllers;

/// <summary>
/// The Merchant API that Payme calls (JSON-RPC 2.0 over HTTP POST, Basic authorization). The protocol wants HTTP 200 for every answer, errors
/// included, so everything is written by hand here. The order of the checks: the caller (address, then login and key) before anything else is read.
/// </summary>
[ApiController]
[Route("api/payme")]
public class PaymeController(PaymeService payme, IConfiguration config, IHostEnvironment env, ILogger<PaymeController> log) : ControllerBase
{
    private const int MaxBody = 64 * 1024;

    // readable UTF-8 instead of \uXXXX escapes (the answer is JSON, never HTML)
    private static readonly JsonSerializerOptions Out = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    [AcceptVerbs("GET", "POST", "PUT", "DELETE", "PATCH")]
    public async Task<IActionResult> Handle()
    {
        JsonElement? id = null;
        try
        {
            var opts = PaymeOptions.From(config);
            if (!Authorized(opts)) throw PaymeException.NoRights();
            if (!HttpMethods.IsPost(Request.Method)) throw PaymeException.NotPost();

            var body = await ReadBodyAsync();
            JsonDocument doc;
            try { doc = JsonDocument.Parse(body); }
            catch (JsonException) { throw PaymeException.ParseError(); }

            using (doc)
            {
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) throw PaymeException.InvalidRequest();
                if (root.TryGetProperty("id", out var rawId) && rawId.ValueKind == JsonValueKind.Number) id = rawId.Clone();
                if (!root.TryGetProperty("method", out var m) || m.ValueKind != JsonValueKind.String) throw PaymeException.InvalidRequest("method");
                if (!root.TryGetProperty("params", out var p) || p.ValueKind != JsonValueKind.Object) throw PaymeException.InvalidRequest("params");

                var name = m.GetString()!;
                var result = await DispatchAsync(name, p);
                log.LogInformation("Payme {Method}: ok", name);
                return Json(new Dictionary<string, object?> { ["result"] = result, ["id"] = id });
            }
        }
        catch (PaymeException ex)
        {
            log.LogInformation("Payme: error {Code}", ex.Code);
            return Error(ex, id);
        }
        catch (Exception ex)
        {
            // never leaks details to the caller; the log has them (and never the Authorization header)
            log.LogError(ex, "Payme: the call failed");
            return Error(PaymeException.Internal(), id);
        }
    }

    private Task<object> DispatchAsync(string method, JsonElement p) => method switch
    {
        "CheckPerformTransaction" => payme.CheckPerformAsync(OrderId(p), Amount(p)),
        "CreateTransaction" => payme.CreateAsync(Text(p, "id"), Long(p, "time"), Amount(p), OrderId(p)),
        "PerformTransaction" => payme.PerformAsync(Text(p, "id")),
        "CancelTransaction" => payme.CancelAsync(Text(p, "id"), (int)Long(p, "reason")),
        "CheckTransaction" => payme.CheckAsync(Text(p, "id")),
        "GetStatement" => payme.StatementAsync(Long(p, "from"), Long(p, "to")),
        _ => throw PaymeException.NoMethod(method),
    };

    // ---- the caller ---------------------------------------------------------------------------------------------------------------------

    private bool Authorized(PaymeOptions opts)
    {
        // Payme's addresses only (on by default in production)
        if (opts.CheckIp ?? env.IsProduction())
        {
            if (!opts.IsAllowed(PaymeClientIp.Of(HttpContext))) return false;
        }
        if (!opts.Enabled) return false;                      // not switched on: nobody gets in

        var header = Request.Headers.Authorization.ToString();
        if (!header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase)) return false;
        string given;
        try { given = Encoding.UTF8.GetString(Convert.FromBase64String(header["Basic ".Length..].Trim())); }
        catch (FormatException) { return false; }

        // compared as hashes of equal length, in constant time: the time of the answer tells nothing about how much was right
        var a = SHA256.HashData(Encoding.UTF8.GetBytes(given));
        var b = SHA256.HashData(Encoding.UTF8.GetBytes($"{opts.Login}:{opts.Key}"));
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    private async Task<string> ReadBodyAsync()
    {
        if (Request.ContentLength > MaxBody) throw PaymeException.InvalidRequest();
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var buffer = new char[MaxBody + 1];
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        if (read > MaxBody) throw PaymeException.InvalidRequest();
        return new string(buffer, 0, read);
    }

    // ---- the parameters -----------------------------------------------------------------------------------------------------------------

    private static string Text(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(v.GetString()) || v.GetString()!.Length > 64)
            throw PaymeException.InvalidRequest(name);
        return v.GetString()!;
    }

    private static long Long(JsonElement p, string name)
    {
        if (!p.TryGetProperty(name, out var v) || v.ValueKind != JsonValueKind.Number || !v.TryGetInt64(out var n)) throw PaymeException.InvalidRequest(name);
        return n;
    }

    private static long Amount(JsonElement p)
    {
        var amount = Long(p, "amount");
        if (amount <= 0) throw PaymeException.WrongAmount();
        return amount;
    }

    /// <summary>The order number of the account; null when it is missing or is not a number (the caller answers "order not found").</summary>
    private static long? OrderId(JsonElement p)
    {
        if (!p.TryGetProperty("account", out var account) || account.ValueKind != JsonValueKind.Object) throw PaymeException.InvalidRequest("account");
        if (!account.TryGetProperty("order_id", out var v)) return null;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetInt64(out var n)) return n > 0 ? n : null;
        if (v.ValueKind == JsonValueKind.String && long.TryParse(v.GetString(), out var s)) return s > 0 ? s : null;
        return null;
    }

    // ---- the answers --------------------------------------------------------------------------------------------------------------------

    private ContentResult Error(PaymeException ex, JsonElement? id) => Json(new Dictionary<string, object?>
    {
        ["error"] = new { code = ex.Code, message = new { ru = ex.Ru, uz = ex.Uz, en = ex.En }, data = ex.Data },
        ["id"] = id,
    });

    private ContentResult Json(object body) => new()
    {
        Content = JsonSerializer.Serialize(body, Out), ContentType = "application/json; charset=utf-8", StatusCode = StatusCodes.Status200OK,
    };
}
