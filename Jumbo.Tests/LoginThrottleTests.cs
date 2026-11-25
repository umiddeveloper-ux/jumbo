using Jumbo.Api.Services;

namespace Jumbo.Tests;

// Protection against guessing passwords: 5 failures lock the account, a lock that comes again is longer.
public class LoginThrottleTests
{
    private static (LoginThrottle throttle, Action<TimeSpan> advance) Make()
    {
        var now = new DateTime(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);
        return (new LoginThrottle(() => now), t => now += t);
    }

    [Fact]
    public void Four_failures_do_not_lock_the_fifth_does()
    {
        var (t, _) = Make();
        var key = LoginThrottle.ForLogin("anna");
        for (var i = 0; i < LoginThrottle.MaxFailures - 1; i++) Assert.Null(t.Fail(key));
        Assert.Equal(TimeSpan.FromMinutes(5), t.Fail(key));
        Assert.NotNull(t.Blocked(key));
    }

    [Fact]
    public void The_login_is_the_same_whatever_the_case()
        => Assert.Equal(LoginThrottle.ForLogin("Anna"), LoginThrottle.ForLogin(" anna "));

    [Fact]
    public void The_lock_ends_and_the_next_one_is_longer()
    {
        var (t, advance) = Make();
        var key = LoginThrottle.ForLogin("anna");
        for (var i = 0; i < 5; i++) t.Fail(key);
        advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));
        Assert.Null(t.Blocked(key));

        TimeSpan? second = null;
        for (var i = 0; i < 5; i++) second = t.Fail(key) ?? second;
        Assert.Equal(TimeSpan.FromMinutes(15), second);
    }

    [Fact]
    public void A_right_password_forgets_the_failures()
    {
        var (t, _) = Make();
        var key = LoginThrottle.ForLogin("anna");
        for (var i = 0; i < 4; i++) t.Fail(key);
        t.Success(key);
        for (var i = 0; i < 4; i++) Assert.Null(t.Fail(key));
    }

    [Fact]
    public void Failures_older_than_the_window_are_not_counted()
    {
        var (t, advance) = Make();
        var key = LoginThrottle.ForLogin("anna");
        for (var i = 0; i < 4; i++) t.Fail(key);
        advance(TimeSpan.FromMinutes(16));
        Assert.Null(t.Fail(key));
        Assert.Null(t.Blocked(key));
    }

    [Fact]
    public void Accounts_are_locked_one_by_one()
    {
        var (t, _) = Make();
        for (var i = 0; i < 5; i++) t.Fail(LoginThrottle.ForLogin("anna"));
        Assert.Null(t.Blocked(LoginThrottle.ForLogin("boris")));
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(61, 2)]
    [InlineData(300, 5)]
    public void Minutes_are_rounded_up_and_never_zero(int seconds, int minutes)
        => Assert.Equal(minutes, LoginThrottle.Minutes(TimeSpan.FromSeconds(seconds)));
}
