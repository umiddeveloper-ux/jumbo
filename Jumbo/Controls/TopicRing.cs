using Microsoft.Maui.Graphics;

namespace Jumbo.Controls;

/// <summary>
/// The round marker of a topic: its number (or a tick) in a circle. A topic that is under way gets a ring of three parts, one per
/// counted game (Find, Listen, Phrase): a won game fills its part with green. Same look as the topic circles of the map.
/// </summary>
public class TopicRing : GraphicsView
{
    public static readonly BindableProperty PartsProperty = BindableProperty.Create(nameof(Parts), typeof(int), typeof(TopicRing), 0, propertyChanged: (b, _, _) => ((TopicRing)b).Invalidate());
    public static readonly BindableProperty StateProperty = BindableProperty.Create(nameof(State), typeof(string), typeof(TopicRing), "future", propertyChanged: (b, _, _) => ((TopicRing)b).Invalidate());
    public static readonly BindableProperty MarkerProperty = BindableProperty.Create(nameof(Marker), typeof(string), typeof(TopicRing), string.Empty, propertyChanged: (b, _, _) => ((TopicRing)b).Invalidate());

    /// <summary>How many of the three parts are filled (0..3).</summary>
    public int Parts { get => (int)GetValue(PartsProperty); set => SetValue(PartsProperty, value); }
    /// <summary>done | current | started | future.</summary>
    public string State { get => (string)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public string Marker { get => (string)GetValue(MarkerProperty); set => SetValue(MarkerProperty, value); }

    public TopicRing()
    {
        Drawable = new RingDrawable(this);
        InputTransparent = true;
    }

    private sealed class RingDrawable(TopicRing owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var c = new PointF(rect.Center.X, rect.Center.Y);
            float r = Math.Min(rect.Width, rect.Height) / 2f - 1f;
            var state = owner.State;
            var fill = state switch
            {
                "done" => Color.FromArgb("#58CC02"),
                "current" => Color.FromArgb("#1CB0F6"),
                "started" => Color.FromArgb("#7A8EA3"),
                _ => Color.FromArgb("#A9BDCE")
            };

            canvas.FillColor = fill;
            canvas.FillCircle(c, r);

            // the ring: three parts with small gaps; a part of a won game is green, the others are pale
            if (state is "started" or "current")
            {
                float ring = Math.Max(3f, r * 0.17f);
                float ar = r - ring / 2f;
                canvas.StrokeSize = ring;
                canvas.StrokeLineCap = LineCap.Butt;
                for (int k = 0; k < 3; k++)
                {
                    canvas.StrokeColor = k < owner.Parts ? Color.FromArgb("#58CC02") : Colors.White.WithAlpha(0.55f);
                    float start = 90f - k * 120f - 4f;       // from the top, clockwise
                    canvas.DrawArc(c.X - ar, c.Y - ar, ar * 2, ar * 2, start, start - 112f, true, false);
                }
                canvas.FillColor = fill;
                canvas.FillCircle(c, r - ring);
            }

            canvas.Font = new Microsoft.Maui.Graphics.Font("NunitoBlack");
            canvas.FontColor = Colors.White;
            canvas.FontSize = owner.Marker.Length > 1 ? 11 : 13;
            canvas.DrawString(owner.Marker, rect.X, rect.Y, rect.Width, rect.Height, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
