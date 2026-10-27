using CommunityToolkit.Maui.Alerts;
using CommunityToolkit.Maui.Core;
using Jumbo.Services;
using Font = Microsoft.Maui.Font;

namespace Jumbo
{
    public partial class AppShell : Shell
    {
        private readonly LivesService _livesService;

        public AppShell(LivesService livesService, AwardWatcher awardWatcher, AdService adService)
        {
            InitializeComponent();
            _livesService = livesService;

            // "You got an award!": ask for new awards after a page is opened, and when the LP grow (a won game)
            Navigated += (_, _) => { awardWatcher.CheckSoon(900); adService.CheckSoon(); UpdateBrand(); Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), UpdateBrand); };       // CurrentPage changes a moment after the event
            Navigated += (_, _) => { ApplyLevelForPage(); Dispatcher.DispatchDelayed(TimeSpan.FromMilliseconds(250), ApplyLevelForPage); };
            SizeChanged += (_, _) => UpdateBrand();
            TitleGrid.SizeChanged += (_, _) => UpdateBrand();
            RightBox.SizeChanged += (_, _) => UpdateBrand();
            int lastLp = -1;
            _livesService.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(LivesService.LpPoints)) return;
                if (lastLp >= 0 && _livesService.LpPoints > lastLp) awardWatcher.CheckSoon(1800);
                lastLp = _livesService.LpPoints;
            };

            UpdateLivesDisplay();
            UpdateThemeIcon();
            _livesService.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName is nameof(LivesService.Lives) or nameof(LivesService.LpPoints) or nameof(LivesService.Streak) or nameof(LivesService.CurrentBook) or nameof(LivesService.RegenCountdown) or nameof(LivesService.Initialized) or nameof(LivesService.ShieldCount) or nameof(LivesService.FreezeCount) or nameof(LivesService.AvatarSvg) or nameof(LivesService.FirstName) or nameof(LivesService.Plan))
                    MainThread.BeginInvokeOnMainThread(UpdateLivesDisplay);
            };
            ThemeService.Changed += () => MainThread.BeginInvokeOnMainThread(UpdateThemeIcon);
            if (Application.Current is { } app) app.RequestedThemeChanged += (_, _) => MainThread.BeginInvokeOnMainThread(UpdateThemeIcon);

            Routing.RegisterRoute("AlphabetPage", typeof(Pages.AlphabetPage));
            Routing.RegisterRoute("SubjectsPage", typeof(Pages.SubjectsPage));
            Routing.RegisterRoute("BookPage", typeof(Pages.BookPage));
            Routing.RegisterRoute("SubjectDetailPage", typeof(Pages.SubjectDetailPage));
            Routing.RegisterRoute("FindMeGamePage", typeof(Pages.FindMeGamePage));
            Routing.RegisterRoute("SequenceGamePage", typeof(Pages.SequenceGamePage));
            Routing.RegisterRoute("ListenGamePage", typeof(Pages.ListenGamePage));
            Routing.RegisterRoute("StepsGamePage", typeof(Pages.StepsGamePage));
            Routing.RegisterRoute("LetterSelectionPage", typeof(Pages.LetterSelectionPage));
            Routing.RegisterRoute("LetterTestPage", typeof(Pages.LetterTestPage));
            Routing.RegisterRoute("LetterRecallPage", typeof(Pages.LetterRecallPage));
            Routing.RegisterRoute("ScanQrPage", typeof(Pages.ScanQrPage));
            Routing.RegisterRoute("LoginPage", typeof(Pages.LoginPage));
            Routing.RegisterRoute("SubjectWordsPage", typeof(Pages.SubjectWordsPage));
            Routing.RegisterRoute("BookGatePage", typeof(Pages.BookGatePage));
            Routing.RegisterRoute("PhraseGamePage", typeof(Pages.PhraseGamePage));
            Routing.RegisterRoute("BookZeroPage", typeof(Pages.BookZeroPage));
            Routing.RegisterRoute("BookZeroGamePage", typeof(Pages.BookZeroGamePage));
            Routing.RegisterRoute("SettingsProfilePage", typeof(Pages.SettingsProfilePage));
            Routing.RegisterRoute("SettingsTeamPage", typeof(Pages.SettingsTeamPage));
            Routing.RegisterRoute("SettingsLearningPage", typeof(Pages.SettingsLearningPage));
            Routing.RegisterRoute("SettingsLookPage", typeof(Pages.SettingsLookPage));
            Routing.RegisterRoute("SettingsBotPage", typeof(Pages.SettingsBotPage));
            Routing.RegisterRoute("SettingsSecurityPage", typeof(Pages.SettingsSecurityPage));
            Routing.RegisterRoute("SettingsAboutPage", typeof(Pages.SettingsAboutPage));
            Routing.RegisterRoute("SettingsDeletePage", typeof(Pages.SettingsDeletePage));

            LocalizationService.RestoreLanguage();

            Dispatcher.Dispatch(async () =>
            {
                if (!UserSession.IsRegistered)
                    await Shell.Current.GoToAsync("//RegistrationPage");
#if DEBUG
                else if (Environment.GetEnvironmentVariable("JUMBO_ROUTE") is { Length: > 0 } route)
                {
                    await Shell.Current.GoToAsync(route);   // screenshots / manual checks, e.g. JUMBO_ROUTE=//settings
                    // A second route after the home page had time to load the player state (lives, LP), e.g. a game.
                    if (Environment.GetEnvironmentVariable("JUMBO_ROUTE2") is { Length: > 0 } route2)
                    {
                        await Task.Delay(9000);
                        await Shell.Current.GoToAsync(route2);
                    }
                }
#endif
            });
        }
        // The word Jumbo is always in the title bar; on a narrow phone (360 dp) or with the back arrow it gets a smaller font, down to 11, so that the numbers and the avatar keep their room.
        private void UpdateBrand()
        {
            double total = TitleGrid.Width;
            if (total <= 0)
            {
                var display = DeviceDisplay.Current.MainDisplayInfo;
                total = display.Density > 0 ? display.Width / display.Density : 360;      // (the grid is not laid out yet)
            }
            double right = ((IView)RightBox).Measure(double.PositiveInfinity, double.PositiveInfinity).Width;
            double room = total - TitleGrid.Padding.HorizontalThickness - right - TitleGrid.ColumnSpacing;
            double size = 19;
            for (; size > 11; size--)
            {
                BrandLabel.FontSize = size;
                if (((IView)BrandLabel).Measure(double.PositiveInfinity, double.PositiveInfinity).Width <= room) break;
            }
            BrandLabel.FontSize = size;
            BrandLabel.IsVisible = true;
        }

        private void UpdateLivesDisplay()
        {
            LivesCountLabel.Text = _livesService.Unlimited ? "∞" : _livesService.Lives.ToString();
            LpCountLabel.Text    = PointsFormat.Compact(_livesService.LpPoints);
            StreakCountLabel.Text = _livesService.Streak.ToString();
            StreakCountLabel.Opacity = _livesService.Streak > 0 ? 1 : 0.5;
            BookCountLabel.Text  = _livesService.CurrentBook.ToString();
            ShieldBox.IsVisible = _livesService.ShieldCount > 0;
            ShieldCountLabel.Text = _livesService.ShieldCount.ToString();
            FreezeBox.IsVisible = _livesService.FreezeCount > 0;
            FreezeCountLabel.Text = _livesService.FreezeCount.ToString();

            // Out of lives and LP: the block clock takes the place of the lives and the LP.
            bool blocked = _livesService.Initialized && _livesService.IsBlocked;
            BlockBox.IsVisible = blocked;
            LivesBox.IsVisible = !blocked;
            LpBox.IsVisible = !blocked;
            BlockClockLabel.Text = _livesService.RegenCountdown;
            LevelLabel.Text      = _livesService.Level;
            AvatarButton.Avatar  = _livesService.AvatarSvg;
            AvatarButton.Initial = _livesService.FirstName;
            PlanCoinView.Plan = _livesService.Plan;
            AvatarBox.IsVisible = UserSession.IsRegistered;
        }

        private async void OnAvatarTapped(object? sender, TappedEventArgs e)
        {
            try { await GoToAsync("//settings"); } catch { /* already there */ }
        }

        // The colour of the level of the book on the pages of a book, a topic and its games; all the other pages keep the orange of the brand.
        private static readonly HashSet<string> LevelPages =
        [
            "BookPage", "SubjectsPage", "SubjectDetailPage", "SubjectWordsPage", "FindMeGamePage", "SequenceGamePage", "ListenGamePage", "StepsGamePage", "PhraseGamePage", "BookGatePage"
        ];

        private void ApplyLevelForPage()
        {
            var name = CurrentPage?.GetType().Name ?? string.Empty;
            var book = string.IsNullOrEmpty(ThemePalette.BookLevel) ? ThemePalette.LevelOfBook(_livesService.CurrentBook) : ThemePalette.BookLevel;
            ThemePalette.SetLevel(LevelPages.Contains(name) ? book : string.Empty);
        }

        private void UpdateThemeIcon() => ThemeIcon.Text = ThemeService.IsDark ? "☀️" : "🌙";

        private void OnThemeTapped(object? sender, TappedEventArgs e) => ThemeService.Toggle();

        public static async Task DisplaySnackbarAsync(string message)
        {
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();

            var snackbarOptions = new SnackbarOptions
            {
                BackgroundColor = Color.FromArgb("#FF3300"),
                TextColor = Colors.White,
                ActionButtonTextColor = Colors.Yellow,
                CornerRadius = new CornerRadius(0),
                Font = Font.SystemFontOfSize(18),
                ActionButtonFont = Font.SystemFontOfSize(14)
            };

            var snackbar = Snackbar.Make(message, visualOptions: snackbarOptions);

            await snackbar.Show(cancellationTokenSource.Token);
        }

        public static async Task DisplayToastAsync(string message)
        {
            // Toast is currently not working in MCT on Windows
            if (OperatingSystem.IsWindows())
                return;

            var toast = Toast.Make(message, textSize: 18);

            var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            await toast.Show(cts.Token);
        }
    }
}
