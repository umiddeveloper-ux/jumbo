using Jumbo.Services;
using Microsoft.Maui.Graphics;

namespace Jumbo.Controls;

/// <summary>A wagon of the train: the letter / number (or "?"), and its state: loco | fixed | gap | next | put.</summary>
public record TrainWagon(string Text, string State);

/// <summary>
/// The train of the game "The missing wagon": a locomotive and seven wagons in a row. Two wagons are empty places (the next one to fill
/// is marked); a wagon that was put in turns green. Drawn in one piece, so it always fits the width.
/// </summary>
public class TrainView : GraphicsView
{
    private readonly TrainDrawable _drawable = new();

    public TrainView() => Drawable = _drawable;

    public void Show(IReadOnlyList<TrainWagon> wagons, bool dark)
    {
        _drawable.Wagons = wagons;
        _drawable.Dark = dark;
        Invalidate();
    }

    private sealed class TrainDrawable : IDrawable
    {
        public IReadOnlyList<TrainWagon> Wagons { get; set; } = [];
        public bool Dark { get; set; }

        public void Draw(ICanvas canvas, RectF rect)
        {
            int n = Wagons.Count;
            if (n == 0) return;

            const float gap = 5f;
            float w = Math.Clamp((rect.Width - gap * (n - 1)) / n, 26f, 50f);
            float h = Math.Min(rect.Height - 8f, w * 1.3f);
            float x = rect.X + (rect.Width - (n * w + (n - 1) * gap)) / 2f;
            float y = rect.Y + (rect.Height - h) / 2f - 2f;

            var primary = ThemePalette.Primary;
            var primaryEdge = ThemePalette.PrimaryEdge;
            var success = Color.FromArgb("#58CC02");
            var successEdge = Color.FromArgb("#46A302");
            var muted = Color.FromArgb(Dark ? "#A1A1AA" : "#6B7280");
            var emptyLine = Color.FromArgb(Dark ? "#34343C" : "#D9DEE6");
            var soft = ThemePalette.PrimarySoft;

            for (int i = 0; i < n; i++, x += w + gap)
            {
                var wagon = Wagons[i];
                var box = new RectF(x, y, w, h);

                if (wagon.State == "loco")
                {
                    canvas.Font = Microsoft.Maui.Graphics.Font.Default;
                    canvas.FontSize = w * 0.85f;
                    canvas.FontColor = Colors.Black;
                    canvas.DrawString("🚂", box.X - 2, box.Y, box.Width + 4, box.Height, HorizontalAlignment.Center, VerticalAlignment.Center);
                    continue;
                }

                // a coupling to the next wagon
                if (i < n - 1)
                {
                    canvas.FillColor = Color.FromArgb("#8A8F98");
                    canvas.FillRectangle(x + w, y + h * 0.62f, gap, 4f);
                }

                float r = Math.Max(6f, w * 0.22f);
                if (wagon.State is "gap" or "next")
                {
                    bool next = wagon.State == "next";
                    if (next)
                    {
                        canvas.FillColor = soft;
                        canvas.FillRoundedRectangle(box, r);
                    }
                    canvas.StrokeColor = next ? primary : emptyLine;
                    canvas.StrokeSize = 3f;
                    canvas.StrokeDashPattern = [2.2f, 1.6f];
                    canvas.DrawRoundedRectangle(box.X + 1.5f, box.Y + 1.5f, box.Width - 3f, box.Height - 3f, r);
                    canvas.StrokeDashPattern = null;
                    canvas.FontColor = next ? primary : muted;
                }
                else
                {
                    bool put = wagon.State == "put";
                    canvas.FillColor = put ? successEdge : primaryEdge;                    // the 3D edge
                    canvas.FillRoundedRectangle(box.X, box.Y + 4f, box.Width, box.Height, r);
                    canvas.FillColor = put ? success : primary;
                    canvas.FillRoundedRectangle(box.X, box.Y, box.Width, box.Height, r);
                    canvas.FontColor = put ? Colors.White : ThemePalette.OnPrimary;
                }

                canvas.Font = new Microsoft.Maui.Graphics.Font("NunitoBlack");
                canvas.FontSize = w * (wagon.Text.Length > 1 ? 0.42f : 0.52f);
                canvas.DrawString(wagon.Text, box.X, box.Y, box.Width, box.Height, HorizontalAlignment.Center, VerticalAlignment.Center);
            }
        }
    }
}
