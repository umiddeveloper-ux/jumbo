using Jumbo.Controls;

namespace Jumbo.Pages;

/// <summary>A screen that cannot be left: the build of the app is older than the minimum the server allows. Only "Update" is on it.</summary>
public class UpdateRequiredPage : ContentPage
{
    public UpdateRequiredPage(string title, string text, string button, Func<Task> update)
    {
        this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F3F5F8"), Color.FromArgb("#0C0C0E"));
        Shell.SetNavBarIsVisible(this, false);

        var titleLabel = new Label { Text = title, FontFamily = "NunitoBlack", FontSize = 26, HorizontalTextAlignment = TextAlignment.Center };
        var textLabel = new Label { Text = text, FontSize = 16, HorizontalTextAlignment = TextAlignment.Center, Opacity = 0.85 };
        var go = new ChunkyButton { Text = button, Kind = "success" };
        go.Clicked += async (_, _) => await update();

        Content = new VerticalStackLayout
        {
            Padding = new Thickness(28, 0),
            Spacing = 18,
            VerticalOptions = LayoutOptions.Center,
            MaximumWidthRequest = 520,
            Children = { new TigerView { Head = true, Size = 120, Mood = "wave", HorizontalOptions = LayoutOptions.Center }, titleLabel, textLabel, go }
        };
    }

    // there is no way back from here
    protected override bool OnBackButtonPressed() => true;
}
