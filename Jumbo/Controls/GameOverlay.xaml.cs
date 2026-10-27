using System.Windows.Input;

namespace Jumbo.Controls;

public partial class GameOverlay : ContentView
{
    /// <summary>win (Jumbo cheers) | lose (Jumbo is sad) | locked (no lives left).</summary>
    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(string), typeof(GameOverlay), "win", propertyChanged: (b, _, _) => ((GameOverlay)b).Apply());

    public static readonly BindableProperty TitleProperty = BindableProperty.Create(
        nameof(Title), typeof(string), typeof(GameOverlay), string.Empty, propertyChanged: (b, _, n) => ((GameOverlay)b).TitleLabel.Text = (string?)n);

    public static readonly BindableProperty MessageProperty = BindableProperty.Create(
        nameof(Message), typeof(string), typeof(GameOverlay), string.Empty, propertyChanged: (b, _, n) =>
        {
            var o = (GameOverlay)b;
            o.MessageLabel.Text = (string?)n;
            o.MessageLabel.IsVisible = !string.IsNullOrEmpty((string?)n);
        });

    public static readonly BindableProperty RewardProperty = BindableProperty.Create(
        nameof(Reward), typeof(string), typeof(GameOverlay), string.Empty, propertyChanged: (b, _, n) =>
        {
            var o = (GameOverlay)b;
            o.RewardLabel.Text = (string?)n;
            o.RewardChip.IsVisible = !string.IsNullOrEmpty((string?)n);
        });

    public static readonly BindableProperty PrimaryTextProperty = BindableProperty.Create(
        nameof(PrimaryText), typeof(string), typeof(GameOverlay), string.Empty, propertyChanged: (b, _, n) =>
        {
            var o = (GameOverlay)b;
            o.PrimaryButton.Text = (string?)n ?? string.Empty;
            o.PrimaryButton.IsVisible = !string.IsNullOrEmpty((string?)n);
        });

    public static readonly BindableProperty PrimaryCommandProperty = BindableProperty.Create(
        nameof(PrimaryCommand), typeof(ICommand), typeof(GameOverlay), null, propertyChanged: (b, _, n) => ((GameOverlay)b).PrimaryButton.Command = n as ICommand);

    public static readonly BindableProperty SecondaryTextProperty = BindableProperty.Create(
        nameof(SecondaryText), typeof(string), typeof(GameOverlay), string.Empty, propertyChanged: (b, _, n) =>
        {
            var o = (GameOverlay)b;
            o.SecondaryButton.Text = (string?)n ?? string.Empty;
            o.SecondaryButton.IsVisible = !string.IsNullOrEmpty((string?)n);
        });

    public static readonly BindableProperty SecondaryCommandProperty = BindableProperty.Create(
        nameof(SecondaryCommand), typeof(ICommand), typeof(GameOverlay), null, propertyChanged: (b, _, n) => ((GameOverlay)b).SecondaryButton.Command = n as ICommand);

    public string Kind { get => (string)GetValue(KindProperty); set => SetValue(KindProperty, value); }
    public string Title { get => (string)GetValue(TitleProperty); set => SetValue(TitleProperty, value); }
    public string Message { get => (string)GetValue(MessageProperty); set => SetValue(MessageProperty, value); }
    public string Reward { get => (string)GetValue(RewardProperty); set => SetValue(RewardProperty, value); }
    public string PrimaryText { get => (string)GetValue(PrimaryTextProperty); set => SetValue(PrimaryTextProperty, value); }
    public ICommand? PrimaryCommand { get => (ICommand?)GetValue(PrimaryCommandProperty); set => SetValue(PrimaryCommandProperty, value); }
    public string SecondaryText { get => (string)GetValue(SecondaryTextProperty); set => SetValue(SecondaryTextProperty, value); }
    public ICommand? SecondaryCommand { get => (ICommand?)GetValue(SecondaryCommandProperty); set => SetValue(SecondaryCommandProperty, value); }

    public GameOverlay()
    {
        InitializeComponent();
        Apply();
        PropertyChanged += (_, e) => { if (e.PropertyName == nameof(IsVisible)) OnShownChanged(); };
    }

    // The panel pops up with a spring; a won game also throws confetti (stopped as soon as the panel goes away).
    private async void OnShownChanged()
    {
        if (!IsVisible) { Confetti.Stop(); return; }
        if (Kind == "win") Confetti.Start(); else Confetti.Stop();
        Panel.Scale = 0.8;
        Panel.Opacity = 0;
        await Task.WhenAll(Panel.FadeToAsync(1, 200), Panel.ScaleToAsync(1, 450, Easing.SpringOut));
    }

    private void Apply()
    {
        Tiger.Mood = Kind == "win" ? "cheer" : "sad";
        TitleLabel.TextColor = Kind switch
        {
            "win" => (Color)Application.Current!.Resources["SuccessText"],
            "lose" => (Color)Application.Current!.Resources["DangerText"],
            _ => (Color)Application.Current!.Resources["Accent"]
        };
        PrimaryButton.Kind = Kind == "win" ? "success" : "primary";

        // The 5-hour block: the title and the clock are in big letters.
        bool locked = Kind == "locked";
        TitleLabel.FontSize = locked ? 32 : 25;
        MessageLabel.FontSize = locked ? 24 : 15;
        MessageLabel.FontFamily = locked ? "NunitoExtraBold" : null;
    }
}
