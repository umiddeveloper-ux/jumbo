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
    private readonly ConfettiDrawable _confetti = new();
    private IDispatcherTimer? _timer;
    private bool _closing;

    public Task<bool> Result => _done.Task;

    public AwardPopupPage(AwardItem award)
    {
        InitializeComponent();
        NameLabel.Text = award.Title;
        BonusLabel.Text = $"+{award.LpBonus} LP";
        BonusLabel.IsVisible = award.LpBonus > 0;
        MedalPicture.Source = new HtmlWebViewSource { Html = SvgHtmlWrapper.Wrap(award.SvgImage).Replace("background:transparent", "background:#FFF0B3") };
        Confetti.Drawable = _confetti;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        StartConfetti();

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

    private void StartConfetti()
    {
        _timer = Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick += (_, _) =>
        {
            _confetti.Step((float)Confetti.Width, (float)Confetti.Height);
            Confetti.Invalidate();
        };
        _timer.Start();
    }

    private void Finish(bool openAwards)
    {
        if (_closing) return;
        _closing = true;
        _timer?.Stop();
        _done.TrySetResult(openAwards);
    }

    private void OnCloseClicked(object? sender, EventArgs e) => Finish(false);

    private void OnOpenClicked(object? sender, EventArgs e) => Finish(true);

    protected override bool OnBackButtonPressed()
    {
        Finish(false);
        return true;
    }

    /// <summary>Pieces of paper falling down the whole window.</summary>
    private sealed class ConfettiDrawable : IDrawable
    {
        private static readonly Color[] Colors =
        [
            Color.FromArgb("#FFC800"), Color.FromArgb("#1CB0F6"), Color.FromArgb("#58CC02"),
            Color.FromArgb("#FF6B9D"), Color.FromArgb("#FF9600"), Color.FromArgb("#A06CF0")
        ];

        private sealed class Piece { public float X, Y, Speed, Spin, Angle, W, H; public int Color; }

        private readonly Random _rnd = new();
        private readonly List<Piece> _pieces = [];

        public void Step(float width, float height)
        {
            if (width <= 0 || height <= 0) return;
            if (_pieces.Count == 0)
            {
                for (int i = 0; i < 44; i++) _pieces.Add(New(width, height, scatter: true));
            }
            foreach (var p in _pieces)
            {
                p.Y += p.Speed;
                p.Angle += p.Spin;
                p.X += (float)Math.Sin(p.Y / 40f) * 0.8f;
                if (p.Y > height + 20)
                {
                    var n = New(width, height, scatter: false);
                    p.X = n.X; p.Y = n.Y; p.Speed = n.Speed; p.Spin = n.Spin; p.Color = n.Color;
                }
            }
        }

        private Piece New(float width, float height, bool scatter) => new()
        {
            X = _rnd.NextSingle() * width,
            Y = scatter ? _rnd.NextSingle() * -height : -20f,
            Speed = 3f + _rnd.NextSingle() * 4f,
            Spin = (_rnd.NextSingle() - 0.5f) * 14f,
            Angle = _rnd.NextSingle() * 360f,
            W = 7f + _rnd.NextSingle() * 4f,
            H = 12f + _rnd.NextSingle() * 6f,
            Color = _rnd.Next(Colors.Length)
        };

        public void Draw(ICanvas canvas, RectF rect)
        {
            foreach (var p in _pieces)
            {
                canvas.SaveState();
                canvas.Translate(p.X, p.Y);
                canvas.Rotate(p.Angle);
                canvas.FillColor = Colors[p.Color];
                canvas.FillRoundedRectangle(-p.W / 2f, -p.H / 2f, p.W, p.H, 2f);
                canvas.RestoreState();
            }
        }
    }
}
