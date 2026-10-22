using Jumbo.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Jumbo
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
            ThemeService.Apply();
#if DEBUG
            ResetAppData();
#endif
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var livesService = IPlatformApplication.Current!.Services.GetRequiredService<LivesService>();
            var awardWatcher = IPlatformApplication.Current.Services.GetRequiredService<AwardWatcher>();
            var adService = IPlatformApplication.Current.Services.GetRequiredService<AdService>();
            var window = new Window(new AppShell(livesService, awardWatcher, adService));
#if WINDOWS
            // Handy for checking the phone layout on a PC: set JUMBO_PHONE=1 to open a phone-sized window.
            if (Environment.GetEnvironmentVariable("JUMBO_PHONE") == "1")
            {
                window.Width = 430;
                window.Height = double.TryParse(Environment.GetEnvironmentVariable("JUMBO_HEIGHT"), out var jh) ? jh : 900;
                window.X = 40;
                window.Y = 20;
            }
#endif
            return window;
        }

#if DEBUG
        private static void ResetAppData()
        {
            Preferences.Default.Clear();

            var dbPath = Path.Combine(FileSystem.AppDataDirectory, "little_polyglot_test1.db3");
            if (File.Exists(dbPath))
                File.Delete(dbPath);

            // Debug convenience: JUMBO_TOKEN skips registration (a signed-in session for screenshots / tests).
            var token = Environment.GetEnvironmentVariable("JUMBO_TOKEN");
            // Android: the system property that carries environment variables is tiny, so the token may also come from a file.
            var tokenFile = Path.Combine(FileSystem.AppDataDirectory, "jumbo_token.txt");
            if (string.IsNullOrEmpty(token) && File.Exists(tokenFile)) token = File.ReadAllText(tokenFile).Trim();
            if (!string.IsNullOrEmpty(token))
            {
                UserSession.AccessToken = token;
                UserSession.UserId = 1;
            }
        }
#endif
    }
}