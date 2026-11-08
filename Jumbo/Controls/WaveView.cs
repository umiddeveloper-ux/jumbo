using Jumbo.Services;

namespace Jumbo.Controls;

/// <summary>
/// The wave at the bottom of a printed card: a band with a scalloped upper edge (a row of round hills). Put it under the word of the card.
/// <see cref="Fill"/> is the colour of the band (the orange of the brand by default), <see cref="Hills"/> the number of hills.
/// </summary>
public class WaveView : GraphicsView
{
    public static readonly BindableProperty FillProperty = BindableProperty.Create(
        nameof(Fill), typeof(Color), typeof(WaveView), null, propertyChanged: (b, _, _) => ((WaveView)b).Invalidate());
    public static readonly BindableProperty HillsProperty = BindableProperty.Create(
        nameof(Hills), typeof(int), typeof(WaveView), 3, propertyChanged: (b, _, _) => ((WaveView)b).Invalidate());

    public Color? Fill { get => (Color?)GetValue(FillProperty); set => SetValue(FillProperty, value); }
    public int Hills { get => (int)GetValue(HillsProperty); set => SetValue(HillsProperty, value); }

    public WaveView()
    {
        InputTransparent = true;
        Drawable = new WaveDrawable(this);
        ThemePalette.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Invalidate);
    }

    private sealed class WaveDrawable(WaveView owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            int n = Math.Max(1, owner.Hills);
            float hill = Math.Min(rect.Height * 0.35f, rect.Width / n * 0.45f);       // how high the hills rise above the valleys
            float valley = hill * 0.75f;                                              // the valleys are a little below the tops
            float w = rect.Width / n;
            var path = new PathF();
            path.MoveTo(0, rect.Height);
            path.LineTo(0, valley);
            for (int i = 0; i < n; i++)
            {
                float x0 = i * w, x1 = (i + 1) * w;
                path.CurveTo(x0, -hill * 0.15f, x1, -hill * 0.15f, x1, valley);        // a round hill from one valley to the next
            }
            path.LineTo(rect.Width, rect.Height);
            path.Close();
            canvas.FillColor = owner.Fill ?? ThemePalette.Brand;
            canvas.FillPath(path);
        }
    }
}
