using CommunityToolkit.Mvvm.ComponentModel;
using Jumbo.Shared.Dtos;

namespace Jumbo.Services;

public partial class LivesService : ObservableObject
{
    public const int MaxLives = 5;
    private const int RegenHours = 5;

    private readonly LpApiClient _apiClient;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LivesDisplay), nameof(CanPlay), nameof(IsBlocked))]
    private int _lives;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(LpDisplay), nameof(CanPlay), nameof(IsBlocked))]
    private int _lpPoints;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RegenCountdown), nameof(RegenMessage))]
    private DateTime _lifeRegenAt;

    // The learner's numbers have been read from the server (before that the lives and the LP are only zeros, not a block).
    [ObservableProperty]
    private bool _initialized;

    // Shields bought in the market: each takes one mistake (no life, no LP is lost). Only goes down here; the market adds them.
    [ObservableProperty]
    private int _shieldCount;

    // Days in a row the daily goal was reached (0 once a day is missed); shown in the title bar.
    [ObservableProperty]
    private int _streak;

    // The book the learner is in and the level that follows it; shown in the title bar next to lives, LP and the streak.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Level))]
    private int _currentBook = 1;

    public string Level => Jumbo.Shared.LanguageLevels.ForBook(CurrentBook);

    public bool CanPlay => Unlimited || Lives > 0 || LpPoints > 0;
    public bool IsBlocked => !Unlimited && Lives <= 0 && LpPoints <= 0;

    public string LivesDisplay => Lives > 0
        ? $"❤️ {Lives}"
        : LpPoints > 0 ? $"🖤 0  💰{LpPoints}" : "🖤 0";

    public string LpDisplay => $"💎 {PointsFormat.Compact(LpPoints)} LP";

    // Time left of the block (zero when there is none) and the same as a clock: 4:59:32.
    public TimeSpan RegenRemaining => LifeRegenAt == default ? TimeSpan.Zero : (LifeRegenAt - DateTime.UtcNow is var r && r > TimeSpan.Zero ? r : TimeSpan.Zero);

    public string RegenCountdown
    {
        get
        {
            var r = RegenRemaining;
            return $"{(int)r.TotalHours}:{r.Minutes:D2}:{r.Seconds:D2}";
        }
    }

    // The text of the block panel: the clock, then what comes back when it is over (the lives; the LP stay as they are).
    public string RegenMessage =>
        RegenCountdown + "\n\n" + LocalizationManager.Format(LpPoints > 0 ? "Block_SubLives" : "Block_SubAll", MaxLives);

    private IDispatcherTimer? _clock;

    // While the block lasts the clock runs; when it is over the lives come back.
    private void StartClock()
    {
        if (_clock is not null || Application.Current?.Dispatcher is not { } dispatcher) return;
        _clock = dispatcher.CreateTimer();
        _clock.Interval = TimeSpan.FromSeconds(1);
        _clock.Tick += (_, _) =>
        {
            if (Lives > 0 || LifeRegenAt == default) return;
            OnPropertyChanged(nameof(RegenCountdown));
            OnPropertyChanged(nameof(RegenMessage));
            if (RegenRemaining <= TimeSpan.Zero) _ = CheckRegenAsync();
        };
        _clock.Start();
    }

    public LivesService(LpApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    // The learner's picture and first name for the title bar (the picture is an svg text, a data: photo or empty).
    [ObservableProperty]
    private string? _avatarSvg;

    [ObservableProperty]
    private string _firstName = string.Empty;

    // The version of the app in force: "bronze" (free), "silver", "gold" and the date a paid one is paid until. Silver and gold have no
    // limits: a mistake costs neither a life nor LP, the lives are shown as ∞.
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Unlimited), nameof(IsBlocked), nameof(CanPlay))]
    private string _plan = Jumbo.Shared.Plans.Bronze;

    [ObservableProperty]
    private DateTime? _planUntil;

    public bool Unlimited => Jumbo.Shared.Plans.NoLimits(Plan);

    // Streak freezes bought in the market (the server uses them up; shown in the header while there are some).
    [ObservableProperty]
    private int _freezeCount;

    /// <param name="known">The learner, when the caller has just read him (no second request).</param>
    public async Task InitAsync(User? known = null)
    {
        if (!UserSession.IsRegistered) return;

        var user = known ?? await _apiClient.GetCurrentUserAsync();
        if (user is null) return;

        Lives    = user.LifeCount;
        LpPoints = user.LpPoints;
        LifeRegenAt = user.LifeExpirationDate;
        ShieldCount = user.ShieldCount;
        FreezeCount = user.FreezeCount;
        AvatarSvg = user.AvatarSvg;
        FirstName = user.FirstName ?? string.Empty;
        PlanUntil = user.PlanUntil;
        Plan = Jumbo.Shared.Plans.Effective(user.Plan, user.PlanUntil, DateTime.UtcNow);
        if (Unlimited) { Lives = MaxLives; LifeRegenAt = default; }      // the lives are full (and stay so)
        Initialized = true;
        StartClock();
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
    private static async Task TellLivesOutAsync()
    {
        try
        {
            if (Shell.Current is { } shell)
                await MainThread.InvokeOnMainThreadAsync(() => shell.DisplayAlertAsync("💔 " + LocalizationManager.Get("LivesOut_Title"), LocalizationManager.Get("LivesOut_Text"), LocalizationManager.Get("LivesOut_Ok")));
        }
        catch { /* the notice is a help: never break the game */ }
    }

    public async Task<bool> TakeDamageAsync(bool livesOnly = false)
    {
        if (Unlimited) return true;           // silver / gold: nothing is lost
        try
        {
            var state = await _apiClient.ReportMistakeAsync(livesOnly);
            if (state is null) return CanPlay;
            int before = Lives;
            Apply(state);
            // the last life is gone but there is LP: from now on a mistake takes LP (the learner is told once, when it happens)
            if (!livesOnly && before > 0 && state.Lives <= 0 && state.LpPoints > 0) await TellLivesOutAsync();
            return state.CanPlay;
        }
        catch { return CanPlay; }
    }
}
