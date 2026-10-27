using CommunityToolkit.Maui;
using Microsoft.Extensions.Logging;
using Syncfusion.Maui.Toolkit.Hosting;
using ZXing.Net.Maui.Controls;

namespace Jumbo
{
    public static class MauiProgram
    {
#if WINDOWS
        // WinUI draws a box, a hover tint and an accent underline on text boxes / combo boxes: make them all transparent.
        private static void ClearWinChrome(Microsoft.UI.Xaml.Controls.Control control)
        {
            var none = new Microsoft.UI.Xaml.Thickness(0);
            var clear = new Microsoft.UI.Xaml.Media.SolidColorBrush(Microsoft.UI.Colors.Transparent);
            control.BorderThickness = none;
            control.Background = clear;
            foreach (var key in new[]
            {
                "TextControlBackground", "TextControlBackgroundPointerOver", "TextControlBackgroundFocused",
                "TextControlBorderBrush", "TextControlBorderBrushPointerOver", "TextControlBorderBrushFocused",
                "ComboBoxBackground", "ComboBoxBackgroundPointerOver", "ComboBoxBackgroundPressed", "ComboBoxBackgroundFocused",
                "ComboBoxBorderBrush", "ComboBoxBorderBrushPointerOver", "ComboBoxBorderBrushPressed", "ComboBoxBorderBrushFocused"
            })
                control.Resources[key] = clear;
            control.Resources["TextControlBorderThemeThicknessFocused"] = none;
            control.Resources["ComboBoxBorderThemeThickness"] = none;
        }
#endif

        public static MauiApp CreateMauiApp()
        {
            Environment.SetEnvironmentVariable("SLAVA_UKRAINI", "1");

            var builder = MauiApp.CreateBuilder();
            builder
                .UseMauiApp<App>()
                .UseMauiCommunityToolkit()
                .UseMauiCommunityToolkitMediaElement()
                .ConfigureSyncfusionToolkit()
                .UseBarcodeReader()
                .ConfigureMauiHandlers(handlers =>
                {
#if WINDOWS
                    Microsoft.Maui.Controls.Handlers.Items.CollectionViewHandler.Mapper.AppendToMapping("KeyboardAccessibleCollectionView", (handler, view) =>
                    {
                        handler.PlatformView.SingleSelectionFollowsFocus = false;
                    });
#endif

                    // Entries and pickers sit inside our own rounded frames (FormEntry / FormPicker): drop the
                    // native box, underline and focus line so only the Jumbo frame is visible.
                    Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("JumboNoChrome", (handler, view) =>
                    {
#if ANDROID
                        handler.PlatformView.Background = null;
                        handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                        handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                        ClearWinChrome(handler.PlatformView);
#endif
                    });
                    Microsoft.Maui.Handlers.PickerHandler.Mapper.AppendToMapping("JumboNoChrome", (handler, view) =>
                    {
#if ANDROID
                        handler.PlatformView.Background = null;
                        handler.PlatformView.SetPadding(0, 0, 0, 0);
#elif IOS || MACCATALYST
                        handler.PlatformView.BorderStyle = UIKit.UITextBorderStyle.None;
#elif WINDOWS
                        ClearWinChrome(handler.PlatformView);
#endif
                    });

                })
                .ConfigureFonts(fonts =>
                {
                    fonts.AddFont("Nunito-Regular.ttf", "NunitoRegular");
                    fonts.AddFont("Nunito-Bold.ttf", "NunitoBold");
                    fonts.AddFont("Nunito-ExtraBold.ttf", "NunitoExtraBold");
                    fonts.AddFont("Nunito-Black.ttf", "NunitoBlack");
                    fonts.AddFont("OpenSans-Regular.ttf", "OpenSansRegular");
                    fonts.AddFont("OpenSans-Semibold.ttf", "OpenSansSemibold");
                    fonts.AddFont("SegoeUI-Semibold.ttf", "SegoeSemibold");
                    fonts.AddFont("FluentSystemIcons-Regular.ttf", FluentUI.FontFamily);
                });

#if DEBUG
    		builder.Logging.AddDebug();
    		builder.Services.AddLogging(configure => configure.AddDebug());
#endif
            builder.Services.AddTransient<AuthHeaderHandler>();
            builder.Services.AddTransient<UnauthorizedHandler>();
#if DEBUG
            builder.Services.AddTransient<TimingHandler>();
#endif
            builder.Services.AddHttpClient<LpApiClient>(client =>
            {
                client.BaseAddress = new Uri(ApiConfig.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);   // a dead network must not freeze a screen for the default 100 s
            })
#if DEBUG
            .AddHttpMessageHandler<TimingHandler>()
#endif
            .AddHttpMessageHandler<AuthHeaderHandler>().AddHttpMessageHandler<UnauthorizedHandler>();

            builder.Services.AddSingleton<LivesService>();
            builder.Services.AddSingleton<BookGateService>();
            builder.Services.AddSingleton<AppUpdateService>();
            builder.Services.AddSingleton<ModalErrorHandler>();

            #region PageModels

            builder.Services.AddSingleton<DashboardPageModel>();
            builder.Services.AddSingleton<GamesPageModel>();
            builder.Services.AddTransient<AlphabetPageModel>();
            builder.Services.AddTransient<RatingPageModel>();
            builder.Services.AddSingleton<DictionaryPageModel>();
            builder.Services.AddSingleton<AwardsPageModel>();
            builder.Services.AddSingleton<SettingsPageModel>();
            builder.Services.AddTransient<SubjectsPageModel>();
            builder.Services.AddTransient<SubjectDetailPageModel>();
            builder.Services.AddTransient<FindMeGamePageModel>();
            builder.Services.AddTransient<SequenceGamePageModel>();
            builder.Services.AddTransient<ListenAndFindGamePageModel>();
            builder.Services.AddTransient<StepsGamePageModel>();
            builder.Services.AddTransient<LetterSelectionPageModel>();
            builder.Services.AddTransient<LetterTestPageModel>();
            builder.Services.AddTransient<LetterRecallPageModel>();
            builder.Services.AddTransient<ScanQrPageModel>();
            builder.Services.AddTransient<RegistrationPageModel>();
            builder.Services.AddTransient<SubjectWordsPageModel>();
            builder.Services.AddTransient<BookGatePageModel>();
            builder.Services.AddTransient<BookPageModel>();
            builder.Services.AddTransient<PhraseGamePageModel>();
            builder.Services.AddTransient<BookZeroPageModel>();
            builder.Services.AddSingleton<AwardWatcher>();
            builder.Services.AddSingleton<AdService>();
            builder.Services.AddTransient<BookZeroGamePageModel>();

            #endregion PageModels

            #region Pages

            builder.Services.AddSingleton<Pages.MainPage>();
            builder.Services.AddSingleton<Pages.GamesPage>();
            builder.Services.AddTransient<Pages.AlphabetPage>();
            builder.Services.AddTransient<Pages.RatingPage>();
            builder.Services.AddSingleton<Pages.DictionaryPage>();
            builder.Services.AddSingleton<Pages.AwardsPage>();
            builder.Services.AddSingleton<Pages.SettingsPage>();
            builder.Services.AddTransient<Pages.SettingsProfilePage>();
            builder.Services.AddTransient<Pages.SettingsTeamPage>();
            builder.Services.AddTransient<Pages.SettingsLearningPage>();
            builder.Services.AddTransient<Pages.SettingsLookPage>();
            builder.Services.AddTransient<Pages.SettingsBotPage>();
            builder.Services.AddTransient<Pages.SettingsSecurityPage>();
            builder.Services.AddTransient<Pages.SettingsAboutPage>();
            builder.Services.AddTransient<Pages.SettingsDeletePage>();
            builder.Services.AddTransient<Pages.SubjectsPage>();
            builder.Services.AddTransient<Pages.SubjectDetailPage>();
            builder.Services.AddTransient<Pages.FindMeGamePage>();
            builder.Services.AddTransient<Pages.SequenceGamePage>();
            builder.Services.AddTransient<Pages.ListenGamePage>();
            builder.Services.AddTransient<Pages.StepsGamePage>();
            builder.Services.AddTransient<Pages.LetterSelectionPage>();
            builder.Services.AddTransient<Pages.LetterTestPage>();
            builder.Services.AddTransient<Pages.LetterRecallPage>();
            builder.Services.AddTransient<Pages.RegistrationPage>();
            builder.Services.AddTransient<Pages.ScanQrPage>();
            builder.Services.AddTransient<Pages.LoginPage>();
            builder.Services.AddTransient<Pages.SubjectWordsPage>();
            builder.Services.AddTransient<Pages.BookGatePage>();
            builder.Services.AddTransient<Pages.BookPage>();
            builder.Services.AddTransient<Pages.PhraseGamePage>();
            builder.Services.AddTransient<Pages.BookZeroPage>();
            builder.Services.AddTransient<Pages.BookZeroGamePage>();

            #endregion Pages



            return builder.Build();
        }
    }
}
