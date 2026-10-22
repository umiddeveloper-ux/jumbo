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

    /// <summary>Takes the numbers the server answered with (a mistake, a won game, a look at the state). The only way they change on this side.</summary>
    public void Apply(LivesStateDto? state)
    {
        if (state is null) return;
        Lives = state.Lives;
        LpPoints = state.LpPoints;
        LifeRegenAt = state.LifeRegenAt ?? default;
        ShieldCount = state.ShieldCount;
    }

    /// <summary>Asks the server for the numbers when the lives are due to come back (the block is over) - otherwise there is nothing to ask.</summary>
    public async Task CheckRegenAsync()
    {
        if (!Initialized) return;
        bool due = (Lives <= 0 && LifeRegenAt == default)                                              // no lives and no timer: the server starts it
                || (Lives < MaxLives && LifeRegenAt != default && DateTime.UtcNow >= LifeRegenAt);       // the block is over
        if (!due) return;
        try { Apply(await _apiClient.GetLivesAsync()); }
        catch { /* no connection: asked again at the next look */ }
    }

    /// <summary>
    /// A mistake. The server counts it (a shield, a life, the LP, the block of 5 hours) and answers with the numbers; returns true when the learner
    /// can go on. livesOnly: the check that opens a book - only the lives count there. Without a connection the mistake is not counted.
    /// </summary>
    public async Task<bool> TakeDamageAsync(bool livesOnly = false)
    {
        if (Unlimited) return true;           // silver / gold: nothing is lost
        try
        {
            var state = await _apiClient.ReportMistakeAsync(livesOnly);
            if (state is null) return CanPlay;
            Apply(state);
            return state.CanPlay;
        }
        catch { return CanPlay; }
    }
}
