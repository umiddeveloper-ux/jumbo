using CommunityToolkit.Mvvm.ComponentModel;
using Jumbo.ApiClient;
using Jumbo.Shared.Dtos;

namespace JumboWeb.Services;

public partial class LivesService : ObservableObject
{
    public const int MaxLives  = 5;
    private const int RegenHours = 5;

    private readonly LpApiClient _apiClient;

    // The learner's numbers have been read from the server (the strip under the top bar reads them once per circuit).
    public bool Initialized { get; private set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LivesDisplay), nameof(CanPlay), nameof(IsBlocked))]
    private int _lives;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LpDisplay), nameof(LpExact), nameof(LivesDisplay), nameof(CanPlay), nameof(IsBlocked))]
    private int _lpPoints;

    // Shields bought in the market: each takes one mistake (no life, no LP is lost). Only goes down here; the market adds them.
    [ObservableProperty]
    private int _shieldCount;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegenCountdown))]
    private DateTime _lifeRegenAt;

    // The version in force (bronze / silver / gold). Silver and gold have no limits: a mistake costs neither a life nor LP.
    public string Plan { get; private set; } = Jumbo.Shared.Plans.Bronze;
    public bool Unlimited => Jumbo.Shared.Plans.NoLimits(Plan);

    public bool CanPlay  => Lives > 0 || LpPoints > 0;
    public bool IsBlocked => Lives <= 0 && LpPoints <= 0;

    public string LivesDisplay => Lives > 0
        ? $"❤️ {Lives}"
        : LpPoints > 0 ? $"🖤 0  💰{PointsFormat.Compact(LpPoints)}" : "🖤 0";

    public string LpDisplay => $"💰 {PointsFormat.Compact(LpPoints)} LP";

    // Full number for tooltips (the header shows the compact form once points pass 1000).
    public string LpExact => PointsFormat.Exact(LpPoints);

    public int MaxLivesCount => MaxLives;

    // Time left of the block (zero when there is none) and the same as a clock: 4:59:32.
    public TimeSpan RegenRemaining => LifeRegenAt == default ? TimeSpan.Zero : (LifeRegenAt - DateTime.UtcNow is var r && r > TimeSpan.Zero ? r : TimeSpan.Zero);

    public string RegenClock
    {
        get
        {
            var r = RegenRemaining;
            return $"{(int)r.TotalHours}:{r.Minutes:D2}:{r.Seconds:D2}";
        }
    }

    public string RegenCountdown
    {
        get
        {
            if (LifeRegenAt == default || Lives >= MaxLives) return string.Empty;
            var remaining = LifeRegenAt - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) return "Восстанавливается...";
            return $"{(int)remaining.TotalHours}ч {remaining.Minutes:D2}м";
        }
    }

    public LivesService(LpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    public async Task InitAsync()
    {
        var user = await _apiClient.GetCurrentUserAsync();
        if (user is null) return;

        Lives       = user.LifeCount;
        LpPoints    = user.LpPoints;
        LifeRegenAt = user.LifeExpirationDate;
        ShieldCount = user.ShieldCount;
        Plan        = Jumbo.Shared.Plans.Effective(user.Plan, user.PlanUntil, DateTime.UtcNow);
        if (Unlimited) { Lives = MaxLives; LifeRegenAt = default; }      // the lives are full (and stay so)
        Initialized = true;
        await CheckRegenAsync();
    }

    public async Task CheckRegenAsync()
    {
        // No lives and no timer (old data): the 5 hours start now, otherwise the lives would never come back.
        if (Lives <= 0 && LifeRegenAt == default)
        {
            LifeRegenAt = DateTime.UtcNow.AddHours(RegenHours);
            await PersistAsync();
            return;
        }

        if (Lives >= MaxLives || LifeRegenAt == default || DateTime.UtcNow < LifeRegenAt) return;
        // The block is over: the lives come back, the LP stays as it is (0 when it was spent).
        Lives       = MaxLives;
        LifeRegenAt = default;
        await PersistAsync();
    }

    /// <summary>
    /// A mistake. Returns true when the learner can go on. livesOnly: the check that opens a book - only the lives count there (the LP
    /// does not stand in for them) and without lives the check is failed. Otherwise the LP are spent when the lives are gone.
    /// </summary>
    public async Task<bool> TakeDamageAsync(bool livesOnly = false)
    {
        if (Unlimited) return true;           // silver / gold: nothing is lost
        await CheckRegenAsync();
        var wasEmpty = Lives <= 0 && LpPoints <= 0;

        // A shield takes the mistake (when there would be something to lose).
        if (ShieldCount > 0 && (Lives > 0 || (!livesOnly && LpPoints > 0)))
        {
            ShieldCount--;
            await PersistAsync();
            return livesOnly ? Lives > 0 : CanPlay;
        }

        if (Lives > 0)
        {
            Lives--;
            if (Lives == 0 && LifeRegenAt == default)
                LifeRegenAt = DateTime.UtcNow.AddHours(RegenHours);
        }
        else if (!livesOnly && LpPoints > 0)
        {
            LpPoints = Math.Max(0, LpPoints - 2);
        }

        // Both are gone: the 5-hour block starts now (a full 5 hours from this moment).
        if (!wasEmpty && Lives <= 0 && LpPoints <= 0)
            LifeRegenAt = DateTime.UtcNow.AddHours(RegenHours);

        await PersistAsync();
        return livesOnly ? Lives > 0 : CanPlay;
    }

    public async Task AddLpAsync(int amount)
    {
        LpPoints += amount;
        await PersistAsync();
    }

    private async Task PersistAsync()
    {
        var user = await _apiClient.GetCurrentUserAsync();
        if (user is null) return;

        var request = UpdateUserRequest.FromUser(user);
        request.LifeCount = Lives;
        request.LpPoints = LpPoints;
        request.LifeExpirationDate = LifeRegenAt;
        request.ShieldCount = ShieldCount;

        await _apiClient.UpdateCurrentUserAsync(request);
    }
}
