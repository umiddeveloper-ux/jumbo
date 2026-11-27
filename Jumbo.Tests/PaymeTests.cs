using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Jumbo.Api.Services.Payme;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo.Tests;

// The Merchant API that Payme calls, played the way the Payme sandbox plays it (the scenario list of the documentation), plus the rules of
// our own: the plan is switched on and taken back, the 12 hour timeout, the address of the caller, two calls at once.
public class PaymeTests : IClassFixture<ApiFactory>, IDisposable
{
    private readonly PaymeRig _rig;

    public PaymeTests(ApiFactory api) => _rig = new PaymeRig(api);
    public void Dispose() => _rig.Dispose();

    // A learner with a new order for Silver / month (50 000 so'm = 5 000 000 tiyin).
    private async Task<(int userId, HttpClient client, long orderId, long tiyin)> OrderAsync(string plan = Plans.Silver, string period = Plans.Month)
    {
        var (client, userId, _, _) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(client, plan, period);
        return (userId, client, orderId, sum * 100);
    }

    // ---- the protocol: the caller, the format -------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_wrong_key_is_32504_with_http_200()
    {
        var a = await _rig.CallAsync("CheckTransaction", new { id = PaymeRig.NewPaymeId() }, auth: PaymeRig.Basic(key: "wrong"));
        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(-32504, a.Code);
        Assert.Equal(JsonValueKind.String, a.Error.GetProperty("message").GetProperty("ru").ValueKind);
        Assert.Equal(JsonValueKind.String, a.Error.GetProperty("message").GetProperty("uz").ValueKind);
        Assert.Equal(JsonValueKind.String, a.Error.GetProperty("message").GetProperty("en").ValueKind);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer abc")]
    [InlineData("Basic !!!not-base64!!!")]
    [InlineData("Basic cGxhaW4=")]                 // "plain": no colon
    public async Task No_or_broken_authorization_is_32504(string? header)
    {
        var a = await _rig.CallAsync("CheckTransaction", new { id = PaymeRig.NewPaymeId() }, auth: header);
        Assert.Equal(HttpStatusCode.OK, a.Status);
        Assert.Equal(-32504, a.Code);
    }

    [Fact]
    public async Task Without_keys_set_everybody_gets_32504_and_the_site_hides_the_button()
    {
        using var off = new PaymeRig(_rig.Factory, new() { ["Payme:Login"] = "", ["Payme:Key"] = "" });
        var a = await off.CallAsync("CheckTransaction", new { id = PaymeRig.NewPaymeId() });           // even the header that is "right" elsewhere
        Assert.Equal(-32504, a.Code);

        var config = await off.Factory.CreateClient().GetFromJsonAsync<PaymentsConfigDto>("/api/payments/config");
        Assert.False(config!.Enabled);
        var (client, _, _, _) = await off.NewUserAsync();
        var order = await client.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = "silver", Period = "month" });
        Assert.Equal(HttpStatusCode.Conflict, order.StatusCode);
    }

    [Fact]
    public async Task Only_post_is_accepted_and_the_caller_is_checked_first()
    {
        var get = await _rig.CallAsync("CheckTransaction", new { id = "x" }, verb: HttpMethod.Get);
        Assert.Equal(HttpStatusCode.OK, get.Status);
        Assert.Equal(-32300, get.Code);

        var getWithoutKey = await _rig.CallAsync("CheckTransaction", new { id = "x" }, auth: null, verb: HttpMethod.Get);
        Assert.Equal(-32504, getWithoutKey.Code);
    }

    [Fact]
    public async Task Broken_requests_get_the_json_rpc_errors_with_http_200()
    {
        var notJson = await _rig.CallAsync("x", null, rawBody: "{ this is not json");
        Assert.Equal(HttpStatusCode.OK, notJson.Status);
        Assert.Equal(-32700, notJson.Code);

        Assert.Equal(-32600, (await _rig.CallAsync("x", null, rawBody: "[1,2,3]")).Code);                          // not an object
        Assert.Equal(-32600, (await _rig.CallAsync("x", null, rawBody: "{\"params\":{},\"id\":1}")).Code);        // no method
        Assert.Equal(-32600, (await _rig.CallAsync("x", null, rawBody: "{\"method\":\"CheckTransaction\",\"id\":1}")).Code);        // no params
        Assert.Equal(-32600, (await _rig.CallAsync("x", null, rawBody: "{\"method\":\"CheckTransaction\",\"params\":[],\"id\":1}")).Code);   // params not an object
        Assert.Equal(-32600, (await _rig.CallAsync("CheckTransaction", new { }, id: 1)).Code);                     // no id in the params

        var unknown = await _rig.CallAsync("Explode", new { });
        Assert.Equal(-32601, unknown.Code);
        Assert.Equal("Explode", unknown.Error.GetProperty("data").GetString());
    }

    [Fact]
    public async Task The_id_of_the_request_comes_back()
    {
        var a = await _rig.CallAsync("CheckTransaction", new { id = PaymeRig.NewPaymeId() }, id: 2032);
        Assert.Equal(2032, a.Root.GetProperty("id").GetInt32());
        Assert.Equal(-31003, a.Code);
    }

    // ---- CheckPerformTransaction ---------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Check_perform_unknown_order_is_31050_and_the_message_names_the_field()
    {
        var a = await _rig.CheckPerformAsync(long.MaxValue - 5, 5_000_000);
        Assert.Equal(-31050, a.Code);
        Assert.Equal("order_id", a.Error.GetProperty("data").GetString());
        Assert.False(string.IsNullOrWhiteSpace(a.Error.GetProperty("message").GetProperty("uz").GetString()));

        var noField = await _rig.CallAsync("CheckPerformTransaction", new { amount = 5_000_000, account = new { } });
        Assert.Equal(-31050, noField.Code);
        var text = await _rig.CallAsync("CheckPerformTransaction", new { amount = 5_000_000, account = new { order_id = "abc" } });
        Assert.Equal(-31050, text.Code);
    }

    [Fact]
    public async Task Check_perform_wrong_amount_is_31001()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        Assert.Equal(-31001, (await _rig.CheckPerformAsync(orderId, tiyin - 100)).Code);
        Assert.Equal(-31001, (await _rig.CheckPerformAsync(orderId, tiyin + 100)).Code);
        Assert.Equal(-31001, (await _rig.CheckPerformAsync(orderId, 0)).Code);
        Assert.Equal(-31001, (await _rig.CheckPerformAsync(orderId, 50_000)).Code);       // the amount in so'm instead of tiyin
    }

    [Fact]
    public async Task Check_perform_allows_and_carries_the_fiscal_receipt_data()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        var a = await _rig.CheckPerformAsync(orderId, tiyin);
        Assert.False(a.IsError, a.Body);
        Assert.True(a.Result.GetProperty("allow").GetBoolean());

        var detail = a.Result.GetProperty("detail");
        Assert.Equal(0, detail.GetProperty("receipt_type").GetInt32());
        var item = detail.GetProperty("items")[0];
        Assert.Contains("Silver", item.GetProperty("title").GetString());
        Assert.Equal(tiyin, item.GetProperty("price").GetInt64());               // tiyin per unit
        Assert.Equal(1, item.GetProperty("count").GetInt32());
        Assert.Equal("11111111111111111", item.GetProperty("code").GetString());
        Assert.Equal("1234567", item.GetProperty("package_code").GetString());
        Assert.Equal(12, item.GetProperty("vat_percent").GetInt32());
    }

    [Fact]
    public async Task Check_perform_leaves_the_receipt_out_when_the_codes_are_not_set()
    {
        using var noCodes = new PaymeRig(_rig.Factory, new() { ["Payme:Mxik"] = "" });
        var (client, _, _, _) = await noCodes.NewUserAsync();
        var (orderId, sum, _) = await noCodes.NewOrderAsync(client);
        var a = await noCodes.CheckPerformAsync(orderId, sum * 100);
        Assert.True(a.Result.GetProperty("allow").GetBoolean());
        Assert.False(a.Result.TryGetProperty("detail", out _));
    }

    // ---- CreateTransaction ---------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task Create_wrong_amount_and_unknown_order_are_refused()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        Assert.Equal(-31001, (await _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin + 1)).Code);
        Assert.Equal(-31050, (await _rig.CreateAsync(PaymeRig.NewPaymeId(), long.MaxValue - 7, tiyin)).Code);
        Assert.Equal("new", _rig.OrderStatus(orderId));
    }

    [Fact]
    public async Task Create_makes_a_transaction_in_state_1_and_a_repeat_gets_the_same_answer()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();

        var first = await _rig.CreateAsync(id, orderId, tiyin, time: 1_700_000_000_000);
        Assert.False(first.IsError, first.Body);
        Assert.Equal(1, first.State);
        Assert.False(string.IsNullOrEmpty(first.Result.GetProperty("transaction").GetString()));
        Assert.True(first.Result.GetProperty("create_time").GetInt64() > 0);
        Assert.Equal("waiting", _rig.OrderStatus(orderId));

        var repeat = await _rig.CreateAsync(id, orderId, tiyin, time: 1_700_000_000_000);
        Assert.Equal(first.ResultText, repeat.ResultText);                       // the same, to the letter

        var check = await _rig.CheckAsync(id);
        Assert.Equal(1, check.State);
        Assert.Equal(0, check.Result.GetProperty("perform_time").GetInt64());
        Assert.Equal(0, check.Result.GetProperty("cancel_time").GetInt64());
        Assert.Equal(JsonValueKind.Null, check.Result.GetProperty("reason").ValueKind);
    }

    [Fact]
    public async Task A_second_transaction_for_an_order_that_waits_is_31008()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        Assert.False((await _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin)).IsError);

        var second = await _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin);
        Assert.Equal(-31008, second.Code);

        // and the check before it says the order is waiting (an account error: the range -31050 ... -31099)
        var check = await _rig.CheckPerformAsync(orderId, tiyin);
        Assert.InRange(check.Code, -31099, -31050);
    }

    [Fact]
    public async Task Cancelling_a_created_transaction_frees_the_order_for_a_new_payment()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, tiyin);

        var cancel = await _rig.CancelAsync(id, reason: 3);
        Assert.False(cancel.IsError, cancel.Body);
        Assert.Equal(-1, cancel.State);
        Assert.True(cancel.Result.GetProperty("cancel_time").GetInt64() > 0);
        Assert.Equal("new", _rig.OrderStatus(orderId));

        var check = await _rig.CheckAsync(id);
        Assert.Equal(-1, check.State);
        Assert.Equal(3, check.Result.GetProperty("reason").GetInt32());

        // the cancelled one cannot be created again with its old id, but the order takes a new payment
        Assert.Equal(-31008, (await _rig.CreateAsync(id, orderId, tiyin)).Code);
        Assert.False((await _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin)).IsError);
    }

    [Fact]
    public async Task Two_calls_at_once_for_one_order_make_exactly_one_transaction()
    {
        for (var round = 0; round < 5; round++)
        {
            var (_, _, orderId, tiyin) = await OrderAsync();
            var calls = Enumerable.Range(0, 4).Select(_ => Task.Run(() => _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin))).ToArray();
            var answers = await Task.WhenAll(calls);

            Assert.Equal(1, answers.Count(a => !a.IsError));
            Assert.All(answers.Where(a => a.IsError), a => Assert.Equal(-31008, a.Code));
            Assert.Equal("waiting", _rig.OrderStatus(orderId));
        }
    }

    [Fact]
    public async Task The_same_payment_sent_twice_at_once_is_one_transaction()
    {
        var (_, _, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();
        var answers = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => Task.Run(() => _rig.CreateAsync(id, orderId, tiyin, time: 1_700_000_000_000))));
        Assert.All(answers, a => Assert.False(a.IsError, a.Body));
        Assert.Single(answers.Select(a => a.ResultText).Distinct());
    }

    // ---- PerformTransaction: the version is switched on -----------------------------------------------------------------------------------

    [Fact]
    public async Task Perform_switches_the_version_on_and_a_repeat_changes_nothing()
    {
        var (userId, client, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, tiyin);

        var before = DateTime.UtcNow;
        var perform = await _rig.PerformAsync(id);
        Assert.False(perform.IsError, perform.Body);
        Assert.Equal(2, perform.State);
        Assert.True(perform.Result.GetProperty("perform_time").GetInt64() > 0);

        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Silver, plan);
        Assert.InRange(until!.Value, before.AddMonths(1).AddMinutes(-1), DateTime.UtcNow.AddMonths(1).AddMinutes(1));
        Assert.Equal("paid", _rig.OrderStatus(orderId));

        var repeat = await _rig.PerformAsync(id);
        Assert.Equal(perform.ResultText, repeat.ResultText);
        Assert.Equal(until, _rig.PlanOf(userId).until);                         // not lengthened a second time

        var check = await _rig.CheckAsync(id);
        Assert.Equal(2, check.State);
        Assert.True(check.Result.GetProperty("perform_time").GetInt64() > 0);

        // what the learner sees
        var status = await client.GetFromJsonAsync<PaymentOrderDto>($"/api/payments/orders/{orderId}");
        Assert.Equal("paid", status!.Status);
    }

    [Fact]
    public async Task A_year_gives_a_year()
    {
        var (userId, _, orderId, tiyin) = await OrderAsync(Plans.Silver, Plans.Year);
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, tiyin);
        await _rig.PerformAsync(id);
        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Silver, plan);
        Assert.InRange(until!.Value, DateTime.UtcNow.AddYears(1).AddMinutes(-1), DateTime.UtcNow.AddYears(1).AddMinutes(1));
    }

    [Fact]
    public async Task Perform_of_an_unknown_or_cancelled_transaction_is_refused()
    {
        Assert.Equal(-31003, (await _rig.PerformAsync(PaymeRig.NewPaymeId())).Code);

        var (userId, _, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, tiyin);
        await _rig.CancelAsync(id);
        Assert.Equal(-31008, (await _rig.PerformAsync(id)).Code);
        Assert.Equal(Plans.Bronze, _rig.PlanOf(userId).plan);
    }

    [Fact]
    public async Task The_same_version_again_goes_on_from_the_end_and_another_one_starts_now()
    {
        var (userId, client, firstOrder, tiyin) = await OrderAsync();
        var first = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(first, firstOrder, tiyin);
        await _rig.PerformAsync(first);
        var firstEnd = _rig.PlanOf(userId).until!.Value;

        // Silver again: from the end of the first month
        var (secondOrder, sum, _) = await _rig.NewOrderAsync(client, Plans.Silver, Plans.Month);
        var second = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(second, secondOrder, sum * 100);
        await _rig.PerformAsync(second);
        Assert.Equal(Plans.AddPeriod(firstEnd, Plans.Month), _rig.PlanOf(userId).until!.Value, TimeSpan.FromSeconds(1));

        // Gold: starts now, the Silver that was bought is replaced
        var (goldOrder, goldSum, _) = await _rig.NewOrderAsync(client, Plans.Gold, Plans.Month);
        var gold = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(gold, goldOrder, goldSum * 100);
        await _rig.PerformAsync(gold);
        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Gold, plan);
        Assert.InRange(until!.Value, DateTime.UtcNow.AddMonths(1).AddMinutes(-1), DateTime.UtcNow.AddMonths(1).AddMinutes(1));
    }

    [Fact]
    public async Task A_higher_version_in_force_makes_a_lower_one_impossible()
    {
        var (userId, client, silverOrder, silverTiyin) = await OrderAsync();           // the Silver order exists first, a payment of it is started
        var silver = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(silver, silverOrder, silverTiyin);
        var (laterOrder, laterSum, _) = await _rig.NewOrderAsync(client);              // and a second Silver order that nobody has started to pay

        // Gold is bought meanwhile
        var (goldOrder, goldSum, _) = await _rig.NewOrderAsync(client, Plans.Gold, Plans.Month);
        var gold = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(gold, goldOrder, goldSum * 100);
        await _rig.PerformAsync(gold);

        // the Silver payment that was started earlier cannot be completed now
        Assert.Equal(-31008, (await _rig.PerformAsync(silver)).Code);
        Assert.Equal(Plans.Gold, _rig.PlanOf(userId).plan);

        // and a new Silver order is refused by the site and by Payme's check
        var again = await client.PostAsJsonAsync("/api/payments/orders", new CreatePaymentOrderRequest { Plan = "silver", Period = "month" });
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);

        // the order that was not started cannot be paid any more either
        Assert.Equal(-31052, (await _rig.CheckPerformAsync(laterOrder, laterSum * 100)).Code);
        Assert.Equal(-31052, (await _rig.CreateAsync(PaymeRig.NewPaymeId(), laterOrder, laterSum * 100)).Code);
    }

    // ---- CancelTransaction after the payment: a refund -----------------------------------------------------------------------------------

    [Fact]
    public async Task A_refund_takes_the_version_back_and_closes_the_order()
    {
        var (userId, _, orderId, tiyin) = await OrderAsync();
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, tiyin);
        await _rig.PerformAsync(id);
        Assert.Equal(Plans.Silver, _rig.PlanOf(userId).plan);

        var cancel = await _rig.CancelAsync(id, reason: 5);
        Assert.False(cancel.IsError, cancel.Body);
        Assert.Equal(-2, cancel.State);

        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Bronze, plan);
        Assert.Null(until);
        Assert.Equal("cancelled", _rig.OrderStatus(orderId));

        var repeat = await _rig.CancelAsync(id, reason: 5);
        Assert.Equal(cancel.ResultText, repeat.ResultText);

        var check = await _rig.CheckAsync(id);
        Assert.Equal(-2, check.State);
        Assert.Equal(5, check.Result.GetProperty("reason").GetInt32());

        // a refunded order is closed: it cannot be paid again (an account error: the range -31050 ... -31099)
        Assert.InRange((await _rig.CheckPerformAsync(orderId, tiyin)).Code, -31099, -31050);
        Assert.InRange((await _rig.CreateAsync(PaymeRig.NewPaymeId(), orderId, tiyin)).Code, -31099, -31050);
    }

    [Fact]
    public async Task A_refund_of_a_later_payment_puts_back_what_the_learner_had()
    {
        var (userId, client, firstOrder, tiyin) = await OrderAsync();
        var first = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(first, firstOrder, tiyin);
        await _rig.PerformAsync(first);
        var firstEnd = _rig.PlanOf(userId).until!.Value;

        var (secondOrder, sum, _) = await _rig.NewOrderAsync(client);
        var second = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(second, secondOrder, sum * 100);
        await _rig.PerformAsync(second);
        Assert.NotEqual(firstEnd, _rig.PlanOf(userId).until!.Value);

        // the second payment is refunded: the first month stays
        await _rig.CancelAsync(second);
        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Silver, plan);
        Assert.Equal(firstEnd, until!.Value, TimeSpan.FromMilliseconds(1));

        // the first is refunded as well: nothing is left
        await _rig.CancelAsync(first);
        Assert.Equal(Plans.Bronze, _rig.PlanOf(userId).plan);
    }

    [Fact]
    public async Task A_refund_of_the_first_payment_takes_one_period_off_when_a_second_one_was_added()
    {
        var (userId, client, firstOrder, tiyin) = await OrderAsync();
        var first = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(first, firstOrder, tiyin);
        await _rig.PerformAsync(first);
        var (secondOrder, sum, _) = await _rig.NewOrderAsync(client);
        var second = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(second, secondOrder, sum * 100);
        await _rig.PerformAsync(second);
        var bothEnd = _rig.PlanOf(userId).until!.Value;

        // the FIRST one is refunded while the second still counts: one month comes off the end
        await _rig.CancelAsync(first);
        var (plan, until) = _rig.PlanOf(userId);
        Assert.Equal(Plans.Silver, plan);
        Assert.Equal(Plans.SubtractPeriod(bothEnd, Plans.Month), until!.Value, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task Cancelling_an_unknown_transaction_is_31003()
        => Assert.Equal(-31003, (await _rig.CancelAsync(PaymeRig.NewPaymeId())).Code);

    // ---- GetStatement --------------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task The_statement_lists_the_transactions_of_the_window_in_order()
    {
        var baseTime = 4_000_000_000_000L + Random.Shared.NextInt64(1, 500_000_000) * 1000;
        var ids = new List<string>();
        var orders = new List<long>();
        for (var i = 0; i < 3; i++)
        {
            var (_, _, orderId, tiyin) = await OrderAsync();
            var id = PaymeRig.NewPaymeId();
            Assert.False((await _rig.CreateAsync(id, orderId, tiyin, time: baseTime + i * 10)).IsError);
            ids.Add(id);
            orders.Add(orderId);
        }
        await _rig.PerformAsync(ids[0]);

        // from and to belong to the window
        var window = await _rig.CallAsync("GetStatement", new { from = baseTime, to = baseTime + 10 });
        var list = window.Result.GetProperty("transactions");
        Assert.Equal(2, list.GetArrayLength());
        Assert.Equal(ids[0], list[0].GetProperty("id").GetString());
        Assert.Equal(ids[1], list[1].GetProperty("id").GetString());

        var item = list[0];
        Assert.Equal(baseTime, item.GetProperty("time").GetInt64());
        Assert.Equal(5_000_000, item.GetProperty("amount").GetInt64());
        Assert.Equal(orders[0].ToString(), item.GetProperty("account").GetProperty("order_id").GetString());
        Assert.True(item.GetProperty("create_time").GetInt64() > 0);
        Assert.True(item.GetProperty("perform_time").GetInt64() > 0);
        Assert.Equal(0, item.GetProperty("cancel_time").GetInt64());
        Assert.False(string.IsNullOrEmpty(item.GetProperty("transaction").GetString()));
        Assert.Equal(2, item.GetProperty("state").GetInt32());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("reason").ValueKind);
        Assert.Equal(JsonValueKind.Null, item.GetProperty("receivers").ValueKind);
        Assert.Equal(JsonValueKind.Null, list[1].GetProperty("reason").ValueKind);
        Assert.Equal(1, list[1].GetProperty("state").GetInt32());

        var all = await _rig.CallAsync("GetStatement", new { from = baseTime - 5, to = baseTime + 25 });
        Assert.Equal(3, all.Result.GetProperty("transactions").GetArrayLength());

        // an empty window is an empty list, not an error
        var empty = await _rig.CallAsync("GetStatement", new { from = baseTime + 11, to = baseTime + 19 });
        Assert.Equal("{\"transactions\":[]}", empty.ResultText);
    }

    // ---- the 12 hour timeout -------------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_payment_that_comes_after_12_hours_is_refused_and_the_transaction_is_cancelled_by_timeout()
    {
        using var rig = new PaymeRig(_rig.Factory);
        var (client, userId, _, _) = await rig.NewUserAsync();
        var (orderId, sum, _) = await rig.NewOrderAsync(client);
        var id = PaymeRig.NewPaymeId();
        await rig.CreateAsync(id, orderId, sum * 100);

        rig.Clock.Advance(TimeSpan.FromHours(11));
        Assert.False((await rig.CheckAsync(id)).IsError);
        rig.Clock.Advance(TimeSpan.FromHours(1) + TimeSpan.FromMinutes(1));

        Assert.Equal(-31008, (await rig.PerformAsync(id)).Code);
        var check = await rig.CheckAsync(id);
        Assert.Equal(-1, check.State);
        Assert.Equal(4, check.Result.GetProperty("reason").GetInt32());
        Assert.Equal(Plans.Bronze, rig.PlanOf(userId).plan);
        Assert.Equal("new", rig.OrderStatus(orderId));
    }

    [Fact]
    public async Task The_job_cancels_the_old_unpaid_transactions_and_leaves_the_rest()
    {
        using var rig = new PaymeRig(_rig.Factory);
        var (client, _, _, _) = await rig.NewUserAsync();
        var (oldOrder, sum, _) = await rig.NewOrderAsync(client);
        var oldId = PaymeRig.NewPaymeId();
        await rig.CreateAsync(oldId, oldOrder, sum * 100);

        var (paidOrder, paidSum, _) = await rig.NewOrderAsync(client, Plans.Gold, Plans.Month);
        var paidId = PaymeRig.NewPaymeId();
        await rig.CreateAsync(paidId, paidOrder, paidSum * 100);
        await rig.PerformAsync(paidId);

        rig.Clock.Advance(TimeSpan.FromHours(13));

        using var scope = rig.Factory.Services.CreateScope();
        var cancelled = await scope.ServiceProvider.GetRequiredService<PaymeService>().ExpireAsync();
        Assert.True(cancelled >= 1);

        var old = await rig.CheckAsync(oldId);
        Assert.Equal(-1, old.State);
        Assert.Equal(4, old.Result.GetProperty("reason").GetInt32());
        Assert.Equal(2, (await rig.CheckAsync(paidId)).State);                   // a paid one is never touched
    }

    // ---- the address of the caller -------------------------------------------------------------------------------------------------------

    [Fact]
    public async Task With_the_address_check_on_only_the_addresses_of_payme_get_in()
    {
        using var rig = new PaymeRig(_rig.Factory, new() { ["Payme:CheckIp"] = "true" }, testIp: true);
        var id = new { id = PaymeRig.NewPaymeId() };

        // a stranger with the right key is refused; Payme's addresses are let in (the answer is "not found", which proves the call got through)
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "203.0.113.9")).Code);
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "185.234.113.1")).Code);
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "185.234.113.15")).Code);
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "185.234.113.16")).Code);
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id)).Code);                          // no address at all

        // behind our nginx: the connection is the container, the real address is the LAST entry of X-Forwarded-For
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "172.18.0.4", forwardedFor: "185.234.113.7")).Code);
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "172.18.0.4", forwardedFor: "203.0.113.9, 185.234.113.7")).Code);
        // a caller cannot write himself into the list: what he wrote is first, nginx's own entry is last
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "172.18.0.4", forwardedFor: "185.234.113.7, 203.0.113.9")).Code);
        // a public connection is taken as it is, the header is not believed
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "203.0.113.9", forwardedFor: "185.234.113.7")).Code);

        // the wrong key is still refused from a good address
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "185.234.113.5", auth: PaymeRig.Basic(key: "wrong"))).Code);
    }

    [Fact]
    public async Task The_allowed_addresses_can_be_changed_by_a_setting()
    {
        using var rig = new PaymeRig(_rig.Factory, new() { ["Payme:CheckIp"] = "true", ["Payme:AllowedIps"] = "198.51.100.1, 198.51.100.20-198.51.100.30" }, testIp: true);
        var id = new { id = PaymeRig.NewPaymeId() };
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "198.51.100.1")).Code);
        Assert.Equal(-31003, (await rig.CallAsync("CheckTransaction", id, remoteIp: "198.51.100.25")).Code);
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "198.51.100.2")).Code);
        Assert.Equal(-32504, (await rig.CallAsync("CheckTransaction", id, remoteIp: "185.234.113.5")).Code);
    }

    // ---- the account of a deleted learner ------------------------------------------------------------------------------------------------

    [Fact]
    public async Task A_deleted_account_leaves_the_order_and_the_transaction_for_the_accounts()
    {
        var (client, _, _, password) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(client);
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, sum * 100);
        await _rig.PerformAsync(id);

        var delete = await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = password });
        Assert.True(delete.IsSuccessStatusCode, await delete.Content.ReadAsStringAsync());

        // the order stays without a learner; Payme can still ask about the transaction and refund it
        Assert.Null(_rig.WithDb(db => db.PaymentOrders.Single(o => o.PaymentOrderId == orderId).UserId));
        Assert.Equal(2, (await _rig.CheckAsync(id)).State);
        var cancel = await _rig.CancelAsync(id);
        Assert.Equal(-2, cancel.State);
    }

    [Fact]
    public async Task A_payment_for_a_deleted_account_cannot_be_completed()
    {
        var (client, _, _, password) = await _rig.NewUserAsync();
        var (orderId, sum, _) = await _rig.NewOrderAsync(client);
        var id = PaymeRig.NewPaymeId();
        await _rig.CreateAsync(id, orderId, sum * 100);

        await client.PostAsJsonAsync("/api/users/me/delete", new DeleteAccountRequest { CurrentPassword = password });

        Assert.Equal(-31008, (await _rig.PerformAsync(id)).Code);               // nobody to give the version to
        Assert.Equal(1, (await _rig.CheckAsync(id)).State);
        Assert.Equal(-1, (await _rig.CancelAsync(id)).State);                    // Payme cancels it and gives the money back
    }
}
