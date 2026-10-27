namespace Jumbo.Controls;

/// <summary>
/// Pieces of paper falling down the whole view (a won game, a new award). Put it over the content with InputTransparent, call
/// <see cref="Start"/> when it appears and <see cref="Stop"/> when it goes away (the timer must not run behind a hidden panel).
/// The colours are the brand orange and the four colours of the levels.
/// </summary>
public class ConfettiView : GraphicsView
{
    private static readonly Color[] Palette =
    [
        Color.FromArgb("#F28C0F"), Color.FromArgb("#E5393B"), Color.FromArgb("#FFC21A"),
        Color.FromArgb("#2F9E44"), Color.FromArgb("#7C4DFF"), Color.FromArgb("#FFFFFF")
    ];

    private sealed class Piece { public float X, Y, Speed, Spin, Angle, W, H; public int Color; }

    private readonly Random _rnd = new();
    private readonly List<Piece> _pieces = [];
    private IDispatcherTimer? _timer;

    public ConfettiView()
    {
        InputTransparent = true;
        Drawable = new ConfettiDrawable(_pieces);
    }

    public void Start()
    {
        if (_timer is { IsRunning: true }) return;
        _pieces.Clear();
        _timer ??= Dispatcher.CreateTimer();
        _timer.Interval = TimeSpan.FromMilliseconds(33);
        _timer.Tick -= OnTick;
        _timer.Tick += OnTick;
        _timer.Start();
    }

    public void Stop()
    {
        _timer?.Stop();
        _pieces.Clear();
        Invalidate();
    }

    private void OnTick(object? sender, EventArgs e)
    {
        Step((float)Width, (float)Height);
        Invalidate();
    }

    private void Step(float width, float height)
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
        Color = _rnd.Next(Palette.Length)
    };

    private sealed class ConfettiDrawable(List<Piece> pieces) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            foreach (var p in pieces)
            {
                canvas.SaveState();
                canvas.Translate(p.X, p.Y);
                canvas.Rotate(p.Angle);
                canvas.FillColor = Palette[p.Color];
                canvas.FillRoundedRectangle(-p.W / 2f, -p.H / 2f, p.W, p.H, 2f);
                canvas.RestoreState();
            }
        }
    }
}
