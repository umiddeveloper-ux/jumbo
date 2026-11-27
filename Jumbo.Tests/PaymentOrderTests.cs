using System.Net;
using System.Net.Http.Json;
using System.Text;
using Jumbo.Api.Services.Payme;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace Jumbo.Tests;

// The orders of the site (what the "Pay with Payme" button does), the settings and the small helpers of the payment.
public class PaymentOrderTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly PaymeRig _rig;

    public PaymentOrderTests(ApiFactory api) => _rig = new PaymeRig(api);
    public void Dispose() => _rig.Dispose();

    private sealed record ErrorBody(string? Error);

    private static async Task<string> ErrorOf(HttpResponseMessage r) => (await r.Content.ReadFromJsonAsync<ErrorBody>())?.Error ?? string.Empty;

    [Fact]
    public async Task The_config_tells_the_site_whether_the_payment_is_on()
    {
        var config = await _rig.Factory.CreateClient().GetFromJsonAsync<PaymentsConfigDto>("/api/payments/config");
        Assert.True(config!.Enabled);
        Assert.True(config.MerchantConfigured);
    }

    [Fact]
    public async Task An_order_takes_its_amount_from_the_plan_prices_and_never_from_the_client()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(client, Plans.Silver, Plans.Year);
        Assert.Equal(500_000, sum);

        var order = _rig.WithDb(db => db.PaymentOrders.AsNoTracking().Single(o => o.PaymentOrderId == orderId));
        Assert.Equal(500_000, order.AmountSum);
        Assert.Equal(Plans.Silver, order.Plan);
        Assert.Equal(Plans.Year, order.Period);
        Assert.Equal("new", order.Status);

        // an extra "amount" in the body is ignored: the request has no such field
        var cheat = await client.PostAsync("/api/payments/orders", new StringContent("{\"plan\":\"gold\",\"period\":\"month\",\"amount\":1,\"AmountSum\":1}", Encoding.UTF8, "application/json"));
        var cheated = (await cheat.Content.ReadFromJsonAsync<CreatePaymentOrderResponse>())!;
        Assert.Equal(90_000, cheated.AmountSum);
    }

    [Fact]
    public async Task The_price_of_an_order_does_not_change_when_the_price_list_does()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        // gold / year is the price that these two tests change (no other test uses it, they run one after the other)
        var (orderId, sum, _) = await _rig.NewOrderAsync(client, Plans.Gold, Plans.Year);
        Assert.Equal(900_000, sum);
        try
        {
            _rig.WithDb(db => db.Database.ExecuteSqlInterpolated($"UPDATE plan_prices SET amount = 950000 WHERE plan = {Plans.Gold} AND period = {Plans.Year}"));
            Assert.Equal(900_000, _rig.WithDb(db => db.PaymentOrders.AsNoTracking().Single(o => o.PaymentOrderId == orderId).AmountSum));

            // the old order is not offered again at the new price: the click makes a new one
            var (newOrder, newSum, _) = await _rig.NewOrderAsync(client, Plans.Gold, Plans.Year);
            Assert.NotEqual(orderId, newOrder);
            Assert.Equal(950_000, newSum);
        }
        finally { _rig.SeedPrices(restoreGoldYear: true); }
    }

    [Fact]
    public async Task A_click_that_comes_again_uses_the_same_order_until_the_payment_is_started()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        var (first, sum, _) = await _rig.NewOrderAsync(client);
        var (again, _, _) = await _rig.NewOrderAsync(client);
        Assert.Equal(first, again);

        // a payment is on its way: the next click makes a new order (Payme would refuse a second payment of the first one)
        await _rig.CreateAsync(PaymeRig.NewPaymeId(), first, sum * 100);
        var (next, _, _) = await _rig.NewOrderAsync(client);
        Assert.NotEqual(first, next);

        // another period is another order
        var (year, _, _) = await _rig.NewOrderAsync(client, Plans.Silver, Plans.Year);
        Assert.NotEqual(next, year);
    }

    [Fact]
    public async Task The_payme_page_link_carries_the_cash_register_the_order_the_amount_the_language_and_the_way_back()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        var (orderId, sum, url) = await _rig.NewOrderAsync(client, Plans.Gold, Plans.Month, lang: "uz");

        Assert.StartsWith("https://checkout.paycom.uz/", url);
        var encoded = url["https://checkout.paycom.uz/".Length..];
        Assert.DoesNotContain('/', encoded);
        Assert.DoesNotContain('+', encoded);
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        var parts = text.Split(';').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => p[1]);

        Assert.Equal(PaymeRig.MerchantId, parts["m"]);
        Assert.Equal(orderId.ToString(), parts["ac.order_id"]);
        Assert.Equal((sum * 100).ToString(), parts["a"]);                       // tiyin
        Assert.Equal("uz", parts["l"]);
        Assert.Equal($"https://jumbo.uz/plans/paid/{orderId}", parts["c"]);
        Assert.True(int.Parse(parts["ct"]) >= 15000);
    }

    [Theory]
    [InlineData("ru", "ru")]
    [InlineData("uz", "uz")]
    [InlineData("en", "en")]
    [InlineData("tr", "en")]
    [InlineData(null, "en")]
    public void The_language_of_the_payme_page_is_ru_uz_or_en(string? given, string expected) => Assert.Equal(expected, PaymeOptions.PageLang(given));

    [Fact]
    public async Task The_sandbox_address_can_be_set()
    {
        using var sandbox = new PaymeRig(_rig.Factory, new() { ["Payme:CheckoutUrl"] = "https://test.paycom.uz/", ["Payme:ReturnBaseUrl"] = "http://localhost:5100" });
        var (client, _, _, _) = await sandbox.NewUserAsync();
        var (orderId, _, url) = await sandbox.NewOrderAsync(client);
        Assert.StartsWith("https://test.paycom.uz/", url);
        var text = Encoding.UTF8.GetString(Convert.FromBase64String(url["https://test.paycom.uz/".Length..]));
        Assert.Contains($"c=http://localhost:5100/plans/paid/{orderId}", text);
    }

    [Fact]
    public async Task Bad_requests_are_refused_with_the_codes_of_the_balance_purchase()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        async Task<(HttpStatusCode, string)> Try(string plan, string period)
        {
            var r = await client.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = plan, Period = period });
            return (r.StatusCode, await ErrorOf(r));
        }

        Assert.Equal((HttpStatusCode.BadRequest, "BAD_PLAN"), await Try("bronze", "month"));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_PLAN"), await Try("platinum", "month"));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_PERIOD"), await Try("silver", "week"));
        Assert.Equal((HttpStatusCode.BadRequest, "BAD_PERIOD"), await Try("silver", ""));
    }

    [Fact]
    public async Task A_version_without_a_price_cannot_be_ordered()
    {
        var (client, _, _, _) = await _rig.NewUserAsync();
        try
        {
            _rig.WithDb(db => db.Database.ExecuteSqlInterpolated($"UPDATE plan_prices SET amount = 0 WHERE plan = {Plans.Gold} AND period = {Plans.Year}"));
            var r = await client.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = "gold", Period = "year" });
            Assert.Equal(HttpStatusCode.Conflict, r.StatusCode);
            Assert.Equal("NO_PRICE", await ErrorOf(r));
        }
        finally { _rig.SeedPrices(restoreGoldYear: true); }
    }

    [Fact]
    public async Task Ordering_needs_a_sign_in()
    {
        var anon = _rig.Factory.CreateClient();
        var r = await anon.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = "silver", Period = "month" });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/payments/orders/1")).StatusCode);
    }

    [Fact]
    public async Task An_order_can_be_seen_by_its_owner_only()
    {
        var (owner, _, _, _) = await _rig.NewUserAsync();
        var (stranger, _, _, _) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(owner);

        var seen = await owner.GetFromJsonAsync<PaymentOrderDto>($"/api/payments/orders/{orderId}");
        Assert.Equal("new", seen!.Status);
        Assert.Equal(sum, seen.AmountSum);
        Assert.Null(seen.PaidAt);

        Assert.Equal(HttpStatusCode.NotFound, (await stranger.GetAsync($"/api/payments/orders/{orderId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await owner.GetAsync("/api/payments/orders/9223372036854775000")).StatusCode);

        // the status follows the payment
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, sum * 100);
        Assert.Equal("waiting", (await owner.GetFromJsonAsync<PaymentOrderDto>($"/api/payments/orders/{orderId}"))!.Status);
        await _rig.PerformAsync(id);
        var paid = await owner.GetFromJsonAsync<PaymentOrderDto>($"/api/payments/orders/{orderId}");
        Assert.Equal("paid", paid!.Status);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task A_learner_cannot_make_endless_orders()
    {
        var (client, userId, _, _) = await _rig.NewUserAsync();
        _rig.WithDb(db =>
        {
            for (var i = 0; i < 30; i++)
                db.PaymentOrders.Add(new PaymentOrder { UserId = userId, Plan = Plans.Gold, Period = Plans.Month, AmountSum = 1, Status = PaymentOrderStatus.Waiting });
            db.SaveChanges();
            return 0;
        });
        var r = await client.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = "silver", Period = "month" });
        Assert.Equal(HttpStatusCode.TooManyRequests, r.StatusCode);
        Assert.Equal("TOO_MANY", await ErrorOf(r));
    }

    [Fact]
    public async Task The_admin_sees_the_orders_and_nobody_else_does()
    {
        var (learner, _, login, _) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(learner);
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, sum * 100);

        Assert.Equal(HttpStatusCode.Forbidden, (await learner.GetAsync("/api/admin/payments")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _rig.Factory.CreateClient().GetAsync("/api/admin/payments")).StatusCode);

        var (admin, _, _, _) = await _rig.NewUserAsync(UserRole.Admin);
        var list = await admin.GetFromJsonAsync<List<AdminPaymentDto>>("/api/admin/payments?take=500");
        var row = list!.Single(r => r.OrderId == orderId);
        Assert.Equal(login, row.UserName);
        Assert.Equal(id, row.PaymeId);
        Assert.Equal(1, row.PaymeState);
        Assert.Equal("waiting", row.Status);
    }
}

// What needs no running API.
public class PaymeUnitTests
{
    [Theory]
    [InlineData("185.234.113.1", true)]
    [InlineData("185.234.113.15", true)]
    [InlineData("185.234.113.0", false)]
    [InlineData("185.234.113.16", false)]
    [InlineData("185.234.114.5", false)]
    [InlineData("::ffff:185.234.113.9", true)]          // an IPv4 address that arrived as IPv6
    [InlineData("2001:db8::1", false)]
    public void Payme_addresses_are_the_default_allowed_ones(string ip, bool allowed)
        => Assert.Equal(allowed, new PaymeOptions().IsAllowed(System.Net.IPAddress.Parse(ip)));

    [Fact]
    public void No_address_is_never_allowed() => Assert.False(new PaymeOptions().IsAllowed(null));

    [Theory]
    [InlineData("10.1.2.3", true)]
    [InlineData("172.16.0.1", true)]
    [InlineData("172.31.255.255", true)]
    [InlineData("172.32.0.1", false)]
    [InlineData("192.168.1.1", true)]
    [InlineData("127.0.0.1", true)]
    [InlineData("::1", true)]
    [InlineData("fd00::5", true)]
    [InlineData("185.234.113.5", false)]
    [InlineData("8.8.8.8", false)]
    public void Internal_addresses_are_where_our_own_proxy_can_be(string ip, bool isInternal)
        => Assert.Equal(isInternal, PaymeClientIp.IsInternal(System.Net.IPAddress.Parse(ip)));

    [Fact]
    public void The_caller_behind_our_proxy_is_the_last_entry_of_the_list()
    {
        var context = new DefaultHttpContext();
        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("172.18.0.3");
        context.Request.Headers["X-Forwarded-For"] = "1.1.1.1, 185.234.113.4";
        Assert.Equal("185.234.113.4", PaymeClientIp.Of(context)!.ToString());

        context.Connection.RemoteIpAddress = System.Net.IPAddress.Parse("203.0.113.9");        // a public connection: the header is ignored
        Assert.Equal("203.0.113.9", PaymeClientIp.Of(context)!.ToString());
    }

    [Fact]
    public void The_checkout_link_never_holds_a_slash_or_a_plus()
    {
        var opts = new PaymeOptions { MerchantId = "587f72c72cac0d162c722ae2" };
        for (long order = 1; order < 3000; order++)
        {
            var link = opts.CheckoutLink(order, order * 5_000_000, "ru");
            var encoded = link["https://checkout.paycom.uz/".Length..];
            Assert.DoesNotContain('/', encoded);
            Assert.DoesNotContain('+', encoded);
            Assert.Contains($"ac.order_id={order};", Encoding.UTF8.GetString(Convert.FromBase64String(encoded)));
        }
    }

    [Fact]
    public void The_receipt_data_needs_all_three_codes()
    {
        Assert.False(new PaymeOptions().FiscalConfigured);
        Assert.False(new PaymeOptions { Mxik = "1", PackageCode = "2" }.FiscalConfigured);
        Assert.True(new PaymeOptions { Mxik = "1", PackageCode = "2", VatPercent = 0 }.FiscalConfigured);       // 0 % is a value
    }

    [Fact]
    public void The_payment_is_on_only_with_a_login_and_a_key()
    {
        Assert.False(new PaymeOptions().Enabled);
        Assert.False(new PaymeOptions { Login = "Paycom" }.Enabled);
        Assert.True(new PaymeOptions { Login = "Paycom", Key = "k" }.Enabled);
    }

    [Fact]
    public void The_name_of_the_sale_on_the_receipt_tells_the_version_and_the_period()
    {
        Assert.Equal("Jumbo Silver — 1 oylik obuna", PaymeService.ItemTitle(Plans.Silver, Plans.Month));
        Assert.Equal("Jumbo Gold — 1 yillik obuna", PaymeService.ItemTitle(Plans.Gold, Plans.Year));
    }

    [Fact]
    public void A_purchase_goes_on_from_the_end_of_the_same_version_and_starts_now_for_another()
    {
        var now = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc);
        var until = now.AddDays(10);
        Assert.Equal(until, Plans.PeriodStart(Plans.Silver, Plans.Silver, until, now));
        Assert.Equal(now, Plans.PeriodStart(Plans.Silver, Plans.Gold, until, now));
        Assert.Equal(now, Plans.PeriodStart(Plans.Bronze, Plans.Silver, null, now));
    }

    [Fact]
    public void A_period_taken_back_is_a_month_or_a_year()
    {
        var end = new DateTime(2027, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateTime(2027, 2, 15, 0, 0, 0, DateTimeKind.Utc), Plans.SubtractPeriod(end, Plans.Month));
        Assert.Equal(new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc), Plans.SubtractPeriod(end, Plans.Year));
    }
}
