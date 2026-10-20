using Microsoft.Maui.Graphics;

namespace Jumbo.Controls;

/// <summary>
/// The three-part ring around the picture of a topic (the website's "ring3"): one part per counted game (Find, Listen, Phrase), a part of
/// a won game is green, the others are pale. Only the ring is drawn; the picture lies over it.
/// </summary>
public class IconRing : GraphicsView
{
    public static readonly BindableProperty PartsProperty = BindableProperty.Create(nameof(Parts), typeof(int), typeof(IconRing), 0, propertyChanged: (b, _, _) => ((IconRing)b).Invalidate());

    /// <summary>How many of the three parts are filled (0..3).</summary>
    public int Parts { get => (int)GetValue(PartsProperty); set => SetValue(PartsProperty, value); }

    public IconRing()
    {
        Drawable = new RingDrawable(this);
        InputTransparent = true;
    }

    private sealed class RingDrawable(IconRing owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var c = new PointF(rect.Center.X, rect.Center.Y);
            float ring = Math.Max(4f, Math.Min(rect.Width, rect.Height) * 0.075f);
            float ar = Math.Min(rect.Width, rect.Height) / 2f - ring / 2f - 1f;
            bool dark = Application.Current?.RequestedTheme == AppTheme.Dark;
            var pale = dark ? Color.FromArgb("#3A4A5C") : Color.FromArgb("#D5E0EA");
            canvas.StrokeSize = ring;
            canvas.StrokeLineCap = LineCap.Butt;
            for (int k = 0; k < 3; k++)
            {
                canvas.StrokeColor = k < owner.Parts ? Color.FromArgb("#58CC02") : pale;
                float start = 90f - k * 120f - 4f;       // from the top, clockwise
                canvas.DrawArc(c.X - ar, c.Y - ar, ar * 2, ar * 2, start, start - 112f, true, false);
            }
        }
    }
}
