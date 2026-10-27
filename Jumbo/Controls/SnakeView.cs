using Jumbo.Services;
using Microsoft.Maui.Graphics;

namespace Jumbo.Controls;

/// <summary>A topic on the snake: its name, state (done | current | started | future), number, how many of the three games are won and whether it is locked (the topic before it is not passed).</summary>
public record SnakeNode(string Name, string State, int Number, int Parts, bool Locked = false, string? Svg = null);

/// <summary>
/// The way through one book on a phone: a thick road that winds DOWN the screen. The first topic is on the left, the second one lower on
/// the right, the third lower on the left again, and so on; the road bends from one to the next. The topics are round markers lying on
/// the road: the three-part ring (one part per counted game, a won game is green) and the number in the middle; the name stands beside
/// the marker, on the free side. The part of the road that is passed is green; the topic to play next has a bubble over it. Tapping a
/// topic raises <see cref="NodeTapped"/>. (The website keeps its own snake in rows of four.)
/// </summary>
public class SnakeView : GraphicsView
{
    // The picture is drawn in its own units (a 700-wide frame) and scaled to the width of the view.
    private const float FrameW = 700f;
    private const float XL = 200f, XR = 500f;                 // the two lanes: the centres of the markers on the left and on the right
    private const float Row = 262f, Y0 = 110f, Node = 100f;     // the distance between two topics down the screen, the first one's place, the marker

    private readonly SnakeDrawable _drawable = new();

    public event Action<int>? NodeTapped;

    public SnakeView()
    {
        Drawable = _drawable;
        StartInteraction += OnTouch;
        SizeChanged += (_, _) => FitHeight();
        ThemePalette.Changed += (_, _) => MainThread.BeginInvokeOnMainThread(Invalidate);      // the level of the book changed the colour
    }

    // The pictures of the topics (drawn from their SVG once, kept): a topic that is not passed shows its picture in the circle.
    private static readonly Dictionary<int, Microsoft.Maui.Graphics.IImage> Icons = new();
    private static readonly HashSet<int> Loading = new();

    private void LoadIcons(IReadOnlyList<SnakeNode> nodes)
    {
        foreach (var n in nodes)
        {
            if (string.IsNullOrEmpty(n.Svg) || n.State == "done") continue;
            int key = n.Svg.GetHashCode();
            lock (Icons) { if (Icons.ContainsKey(key) || !Loading.Add(key)) continue; }
            var svg = n.Svg;
            _ = Task.Run(() =>
            {
                try
                {
                    var png = SvgPicture.ToPng(svg, 220);
                    if (png is null) return;
                    using var ms = new MemoryStream(png);
                    var image = Microsoft.Maui.Graphics.Platform.PlatformImage.FromStream(ms);
                    lock (Icons) Icons[key] = image;
                    MainThread.BeginInvokeOnMainThread(Invalidate);
                }
                catch { /* the number stays in the circle */ }
                finally { lock (Icons) Loading.Remove(key); }
            });
        }
    }

    /// <summary>The height of the centre of a topic from the top of the view (0 when the view has no size yet).</summary>
    public double CenterY(int index)
    {
        if (Width <= 0 || index < 0) return 0;
        var g = Geometry.Compute((float)Width, _drawable.Nodes.Count);
        return index < g.Centers.Count ? g.Centers[index].Y : 0;
    }

    public void Show(IReadOnlyList<SnakeNode> nodes, string bubbleText, bool dark)
    {
        LoadIcons(nodes);
        _drawable.Nodes = nodes;
        _drawable.Bubble = bubbleText;
        _drawable.Dark = dark;
        FitHeight();
        Invalidate();
    }

    // The height follows the width (the picture keeps its proportions).
    private void FitHeight()
    {
        if (Width <= 0) return;
        var h = Geometry.Compute((float)Width, _drawable.Nodes.Count).Total;
        if (Math.Abs(HeightRequest - h) > 0.5) HeightRequest = h;
    }

    private void OnTouch(object? sender, TouchEventArgs e)
    {
        if (e.Touches.Length == 0 || Width <= 0) return;
        var p = e.Touches[0];
        var g = Geometry.Compute((float)Width, _drawable.Nodes.Count);
        for (int i = 0; i < g.Centers.Count; i++)
        {
            var c = g.Centers[i];
            // the marker and its name beside it (on the free side)
            bool left = i % 2 == 0;
            float x0 = left ? 0 : c.X - g.S / 2f - 8f * g.K, x1 = left ? c.X + g.S / 2f + 8f * g.K : (float)Width;
            if (p.X >= x0 && p.X <= x1 && Math.Abs(p.Y - c.Y) <= Row * g.K / 2f - 4f)
            {
                NodeTapped?.Invoke(i);
                return;
            }
        }
    }

    /// <summary>Where everything lies for a given width: the same numbers drive the drawing and the touch.</summary>
    private sealed record Geometry(float K, float S, float R, float Total, List<PointF> Centers)
    {
        public static Geometry Compute(float w, int count)
        {
            float k = w / FrameW;
            var centers = new List<PointF>(count);
            for (int i = 0; i < count; i++)
                centers.Add(new PointF((i % 2 == 0 ? XL : XR) * k, (Y0 + i * Row) * k));      // left, right, left... each one lower
            float total = (Y0 + Math.Max(count - 1, 0) * Row + Node / 2f + 36f) * k;
            return new Geometry(k, Node * k, Row / 2f * k, total, centers);
        }
    }

    private sealed class SnakeDrawable : IDrawable
    {
        public IReadOnlyList<SnakeNode> Nodes { get; set; } = [];
        public string Bubble { get; set; } = string.Empty;
        public bool Dark { get; set; }

        public void Draw(ICanvas canvas, RectF rect)
        {
            if (Nodes.Count == 0) return;
            var g = Geometry.Compute(rect.Width, Nodes.Count);
            float k = g.K;

            var surface = Color.FromArgb(Dark ? "#1C1C21" : "#FFFFFF");
            var muted = Color.FromArgb(Dark ? "#A1A1AA" : "#6B7280");
            var text = Color.FromArgb(Dark ? "#F2F2F4" : "#1B1B1F");
            var ringOff = Color.FromArgb(Dark ? "#4A4A55" : "#CBD0D8");
            var green = ThemePalette.Primary;          // the colour of the level of the book (it was green / blue)
            var blue = ThemePalette.Primary;
            var accentText = ThemePalette.PrimaryText;
            var onAccent = ThemePalette.OnPrimary;

            DrawBody(canvas, g);

            float nameSize = Math.Max(12f, 20f * k);
            for (int i = 0; i < Nodes.Count; i++)
            {
                var n = Nodes[i];
                var c = g.Centers[i];
                float r = g.S / 2f;
                float edge = Math.Max(3f, 6f * k);
                canvas.Alpha = n.Locked ? 0.72f : 1f;

                // the halo of the topic to play next
                if (n.State == "current")
                {
                    canvas.FillColor = blue.WithAlpha(0.35f);
                    canvas.FillCircle(c, r + edge + 11f * k);
                }
                // A marker that comes out of the screen: a soft shadow on the road, a darker "thickness" under the disc (like our chunky buttons)
                // and a face with a light from the top left and a small gloss.
                float outer = r + edge, depth = 11f * k;
                for (int sh = 0; sh < 7; sh++)                          // the shadow: layers that grow and fade
                {
                    canvas.FillColor = Colors.Black.WithAlpha(Dark ? 0.07f : 0.055f);
                    canvas.FillCircle(c.X + 2f * k, c.Y + depth + 5f * k + sh * 1.2f * k, outer + sh * 2.6f * k);
                }
                canvas.FillColor = n.State switch
                {
                    "done" => ThemePalette.PrimaryEdge,
                    "current" => ThemePalette.PrimaryEdge,
                    _ => Color.FromArgb(Dark ? "#0B0B0D" : "#AEB4BE")
                };
                canvas.FillCircle(c.X, c.Y + depth, outer);              // the thickness: the part of the disc that is seen from below
                // the face: the lower part a little darker, the upper one lighter (a bevel made of two plain circles: a gradient paint stays set for the next marker)
                canvas.FillColor = Color.FromArgb(Dark ? "#101013" : "#DDE1E8");
                canvas.FillCircle(c, outer);
                canvas.FillColor = Color.FromArgb(Dark ? "#2A2A31" : "#FFFFFF");
                canvas.FillCircle(c.X - 1.2f * k, c.Y - 2.6f * k, outer - 1.8f * k);
                canvas.FillColor = Colors.White.WithAlpha(Dark ? 0.10f : 0.55f);      // the gloss on the upper rim
                canvas.FillEllipse(c.X - outer * 0.62f, c.Y - outer * 0.96f, outer * 1.0f, outer * 0.36f);

                // the ring: three parts with small gaps, one per counted game; a won game is green
                float ring = Math.Max(4f, r * 0.17f);
                float ar = r - ring / 2f;
                canvas.StrokeSize = ring;
                canvas.StrokeLineCap = LineCap.Butt;
                for (int j = 0; j < 3; j++)
                {
                    canvas.StrokeColor = j < n.Parts || n.State == "done" ? green : ringOff;
                    float start = 90f - j * 120f - 4f;                 // from the top, clockwise
                    canvas.DrawArc(c.X - ar, c.Y - ar, ar * 2, ar * 2, start, start - 112f, true, false);
                }

                canvas.FillColor = n.State switch
                {
                    "done" => ThemePalette.PrimarySoft,
                    "current" => ThemePalette.PrimarySoft,
                    _ => surface
                };
                canvas.FillCircle(c, r - ring);

                // In the middle: a tick for a topic that is done; for the others the picture of the topic (the number, when the picture is not
                // ready yet or the topic has none).
                Microsoft.Maui.Graphics.IImage? icon = null;
                if (n.State != "done" && !string.IsNullOrEmpty(n.Svg)) lock (Icons) Icons.TryGetValue(n.Svg.GetHashCode(), out icon);
                if (icon is not null)
                {
                    float ir = r - ring;                                    // the picture fills the inner disc
                    canvas.SaveState();
                    var clip = new PathF();
                    clip.AppendCircle(c.X, c.Y, ir);
                    canvas.ClipPath(clip);
                    canvas.DrawImage(icon, c.X - ir, c.Y - ir, ir * 2, ir * 2);
                    canvas.RestoreState();

                    // the number becomes a small badge on the upper left of the circle
                    float br = r * 0.34f;
                    var bc = new PointF(c.X - r * 0.74f, c.Y - r * 0.74f);
                    canvas.FillColor = Colors.White.WithAlpha(0.95f);
                    canvas.FillCircle(bc, br + 2f * k);
                    canvas.FillColor = n.State == "current" ? blue : Color.FromArgb(Dark ? "#6B6B75" : "#8A8F98");
                    canvas.FillCircle(bc, br);
                    canvas.Font = new Microsoft.Maui.Graphics.Font("NunitoBlack");
                    canvas.FontColor = n.State == "current" ? onAccent : Colors.White;
                    canvas.FontSize = br * (n.Number > 9 ? 1.05f : 1.3f);
                    canvas.DrawString(n.Number.ToString(), bc.X - br, bc.Y - br, br * 2, br * 2, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                else
                {
                    string marker = n.State == "done" ? "✓" : n.Number.ToString();
                    canvas.Font = new Microsoft.Maui.Graphics.Font("NunitoBlack");
                    canvas.FontColor = n.State switch { "done" => accentText, "current" => accentText, _ => n.State == "started" ? text : muted };
                    canvas.FontSize = marker.Length > 1 ? r * 0.62f : r * 0.74f;
                    canvas.DrawString(marker, c.X - r, c.Y - r, r * 2, r * 2, HorizontalAlignment.Center, VerticalAlignment.Center);
                }
                canvas.Alpha = 1f;

                // a lock on a topic that is not open yet
                if (n.Locked)
                {
                    canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                    canvas.FontSize = Math.Max(12f, r * 0.62f);
                    canvas.DrawString("🔒", c.X + r * 0.2f, c.Y + r * 0.15f, r * 0.95f, r * 0.95f, HorizontalAlignment.Center, VerticalAlignment.Center);
                }

                // the name beside the marker, on the free side (left of a left marker, right of a right one), up to two lines
                bool leftLane = i % 2 == 0;
                float gap = 10f * k, boxW = (leftLane ? c.X - r - edge - gap - 6f * k : rect.Width - (c.X + r + edge + gap) - 6f * k);
                float boxX = leftLane ? 6f * k : c.X + r + edge + gap;
                var nameFont = new Microsoft.Maui.Graphics.Font(n.State == "current" ? "NunitoBlack" : "NunitoExtraBold");
                canvas.Font = nameFont;
                canvas.FontColor = n.State == "current" ? accentText : n.State == "future" ? muted : text;
                // the longest word has to fit the box (the measure is of a plain font, the drawing is Nunito: 15 % in reserve)
                float fs = nameSize;
                float widest = n.Name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Max(w => canvas.GetStringSize(w, nameFont, nameSize).Width * 1.15f);
                if (widest > boxW) fs = Math.Max(nameSize * 0.7f, nameSize * boxW / widest);
                canvas.FontSize = fs;
                canvas.DrawString(n.Name, boxX, c.Y - fs * 1.4f, boxW, fs * 2.8f, leftLane ? HorizontalAlignment.Right : HorizontalAlignment.Left,
                    VerticalAlignment.Center, TextFlow.ClipBounds);

                if (n.State == "current" && Bubble.Length > 0) DrawBubble(canvas, c, r + edge, rect.Width, blue, k);
            }
        }

        // The body: through the markers in the order they are played; between two neighbours it is an S-shaped bend (it leaves the marker
        // downwards and arrives from above). The part up to the first topic that is not done is green.
        private void DrawBody(ICanvas canvas, Geometry g)
        {
            float k = g.K;
            var pts = new List<PointF>();
            var at = new int[Nodes.Count];                              // index in pts of each marker
            for (int i = 0; i < Nodes.Count; i++)
            {
                at[i] = pts.Count;
                pts.Add(g.Centers[i]);
                if (i == Nodes.Count - 1) break;
                var a = g.Centers[i];
                var b = g.Centers[i + 1];
                float dy = b.Y - a.Y;
                var c1 = new PointF(a.X, a.Y + dy * 0.62f);
                var c2 = new PointF(b.X, b.Y - dy * 0.62f);
                const int steps = 30;
                for (int t = 1; t < steps; t++)
                {
                    float u = t / (float)steps, v = 1f - u;
                    float x = v * v * v * a.X + 3 * v * v * u * c1.X + 3 * v * u * u * c2.X + u * u * u * b.X;
                    float y = v * v * v * a.Y + 3 * v * v * u * c1.Y + 3 * v * u * u * c2.Y + u * u * u * b.Y;
                    pts.Add(new PointF(x, y));
                }
            }
            if (pts.Count < 2) return;

            var road = Color.FromArgb(Dark ? "#34343C" : "#D9DEE6");
            var roadEdge = Color.FromArgb(Dark ? "#26262C" : "#B8BFCA");
            var on = ThemePalette.Primary;                  // the finished part of the road has the colour of the level
            var onEdge = ThemePalette.PrimaryEdge;

            int firstOpen = -1;
            for (int i = 0; i < Nodes.Count; i++) if (Nodes[i].State != "done") { firstOpen = i; break; }
            int upTo = firstOpen < 0 ? pts.Count - 1 : at[firstOpen];

            canvas.StrokeLineCap = LineCap.Round;
            canvas.StrokeLineJoin = LineJoin.Round;
            Stroke(canvas, Poly(pts, pts.Count - 1), Node * k + 12f * k, roadEdge);
            Stroke(canvas, Poly(pts, pts.Count - 1), Node * k, road);
            if (upTo > 0)
            {
                Stroke(canvas, Poly(pts, upTo), Node * k + 12f * k, onEdge);
                Stroke(canvas, Poly(pts, upTo), Node * k, on);
            }

            // scales: a line of small dots along the middle of the body
            canvas.StrokeDashPattern = [0.1f, 22f * k / Math.Max(7f * k, 3f)];
            Stroke(canvas, Poly(pts, pts.Count - 1), Math.Max(7f * k, 3f), Colors.White.WithAlpha(0.4f));
            canvas.StrokeDashPattern = null;
        }

        private static void Stroke(ICanvas canvas, PathF path, float size, Color color)
        {
            canvas.StrokeSize = size;
            canvas.StrokeColor = color;
            canvas.DrawPath(path);
        }

        private static PathF Poly(List<PointF> pts, int last)
        {
            var path = new PathF(pts[0]);
            for (int i = 1; i <= last; i++) path.LineTo(pts[i]);
            return path;
        }

        private void DrawBubble(ICanvas canvas, PointF c, float r, float width, Color blue, float k)
        {
            var font = new Microsoft.Maui.Graphics.Font("NunitoBlack");
            float fs = Math.Max(11f, 14f * Math.Max(k, 0.8f));
            canvas.Font = font;
            canvas.FontSize = fs;
            var size = canvas.GetStringSize(Bubble, font, fs);
            float w = size.Width + 24f, h = fs + 10f;
            float x = Math.Clamp(c.X - w / 2f, 2f, width - w - 2f);
            float y = c.Y - r - 8f - h;

            canvas.FillColor = blue;
            canvas.FillRoundedRectangle(x, y, w, h, h / 2f);
            var tip = new PathF(c.X - 6f, y + h - 1f);
            tip.LineTo(c.X + 6f, y + h - 1f);
            tip.LineTo(c.X, y + h + 6f);
            tip.Close();
            canvas.FillPath(tip);
            canvas.FontColor = ThemePalette.OnPrimary;
            canvas.DrawString(Bubble, x, y, w, h, HorizontalAlignment.Center, VerticalAlignment.Center);
        }
    }
}
