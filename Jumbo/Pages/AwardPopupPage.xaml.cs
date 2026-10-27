using Jumbo.Shared.Dtos;
using Jumbo.Utilities;

namespace Jumbo.Pages;

/// <summary>
/// The window "You got an award!": confetti falls, the card pops up, the medal spins in and the glow behind it breathes.
/// <see cref="Result"/> is true when the learner chose "Awards" and false for "Close".
/// </summary>
public partial class AwardPopupPage : ContentPage
{
    private readonly TaskCompletionSource<bool> _done = new();
    private bool _closing;

    public Task<bool> Result => _done.Task;

    public AwardPopupPage(AwardItem award)
    {
        InitializeComponent();
        NameLabel.Text = award.Title;
        BonusLabel.Text = $"+{award.LpBonus} LP";
        BonusLabel.IsVisible = award.LpBonus > 0;
        MedalPicture.Source = new HtmlWebViewSource { Html = SvgHtmlWrapper.Wrap(award.SvgImage).Replace("background:transparent", "background:#FFF0B3") };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        Confetti.Start();

        Card.Scale = 0.6;
        Card.TranslationY = 40;
        Medal.Scale = 0.1;
        Medal.Rotation = -200;
        await Task.WhenAll(
            Card.FadeToAsync(1, 250),
            Card.ScaleToAsync(1, 520, Easing.SpringOut),
            Card.TranslateToAsync(0, 0, 520, Easing.SpringOut));
        await Task.WhenAll(
            Medal.ScaleToAsync(1, 800, Easing.SpringOut),
            Medal.RotateToAsync(0, 800, Easing.CubicOut));

        // the glow behind the medal breathes while the window is open
        while (!_closing)
        {
            await Glow.ScaleToAsync(1.12, 900, Easing.SinInOut);
            if (_closing) break;
            await Glow.ScaleToAsync(0.92, 900, Easing.SinInOut);
        }
    }

    private void Finish(bool openAwards)
    {
        if (_closing) return;
        _closing = true;
        Confetti.Stop();
        _done.TrySetResult(openAwards);
    }

    private void OnCloseClicked(object? sender, EventArgs e) => Finish(false);

    private void OnOpenClicked(object? sender, EventArgs e) => Finish(true);

    protected override bool OnBackButtonPressed()
    {
        Finish(false);
        return true;
    }
}
