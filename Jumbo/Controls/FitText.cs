using Microsoft.Maui.Graphics;
using Font = Microsoft.Maui.Graphics.Font;

namespace Jumbo.Controls;

/// <summary>
/// A short text that always fits its box: it is drawn in one line when it fits, otherwise in two lines (split at a space), and the font
/// shrinks until the text fits the width and the height. A Label inside a card of the games was clipped on Android (long Russian
/// words lost their ends), so the words of the cards are drawn here.
/// </summary>
public class FitText : GraphicsView
{
    public static readonly BindableProperty TextProperty = BindableProperty.Create(nameof(Text), typeof(string), typeof(FitText), string.Empty,
        propertyChanged: (b, _, _) => ((FitText)b).Invalidate());
    public static readonly BindableProperty TextColorProperty = BindableProperty.Create(nameof(TextColor), typeof(Color), typeof(FitText), Colors.Black,
        propertyChanged: (b, _, _) => ((FitText)b).Invalidate());
    public static readonly BindableProperty FontFamilyProperty = BindableProperty.Create(nameof(FontFamily), typeof(string), typeof(FitText), "NunitoRegular",
        propertyChanged: (b, _, _) => ((FitText)b).Invalidate());
    public static readonly BindableProperty MaxFontSizeProperty = BindableProperty.Create(nameof(MaxFontSize), typeof(double), typeof(FitText), 15.0,
        propertyChanged: (b, _, _) => ((FitText)b).Invalidate());
    public static readonly BindableProperty MinFontSizeProperty = BindableProperty.Create(nameof(MinFontSize), typeof(double), typeof(FitText), 9.0,
        propertyChanged: (b, _, _) => ((FitText)b).Invalidate());

    public string Text { get => (string)GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public Color TextColor { get => (Color)GetValue(TextColorProperty); set => SetValue(TextColorProperty, value); }
    public string FontFamily { get => (string)GetValue(FontFamilyProperty); set => SetValue(FontFamilyProperty, value); }
    public double MaxFontSize { get => (double)GetValue(MaxFontSizeProperty); set => SetValue(MaxFontSizeProperty, value); }
    public double MinFontSize { get => (double)GetValue(MinFontSizeProperty); set => SetValue(MinFontSizeProperty, value); }

    public FitText()
    {
        Drawable = new FitDrawable(this);
        InputTransparent = true;
    }

    private sealed class FitDrawable(FitText owner) : IDrawable
    {
        public void Draw(ICanvas canvas, RectF rect)
        {
            var text = owner.Text?.Trim();
            if (string.IsNullOrEmpty(text) || rect.Width <= 0 || rect.Height <= 0) return;

            var font = new Font(owner.FontFamily);
            float max = (float)owner.MaxFontSize, min = (float)owner.MinFontSize;
            canvas.FontColor = owner.TextColor;
            canvas.Font = font;

            for (float fs = max; fs >= min - 0.01f; fs -= 0.5f)
            {
                var lines = Layout(canvas, font, fs, rect.Width, text);
                if (lines is null || lines.Count * fs * 1.2f > rect.Height + 0.5f) continue;
                Paint(canvas, rect, lines, fs);
                return;
            }
            // nothing fits (a very long word): the smallest font, in two lines when there is a space, clipped to the box
            Paint(canvas, rect, SplitInTwo(canvas, font, min, text) ?? [text], min);
        }

        private static void Paint(ICanvas canvas, RectF rect, List<string> lines, float fs)
        {
            float lineHeight = fs * 1.2f;
            float top = rect.Top + 1f;          // from the top of the box: the pictures above have the same top line in every card
            canvas.FontSize = fs;
            // each line in a box three times as wide as the view and centred on it: the drawing never wraps a word by itself
            for (int i = 0; i < lines.Count; i++)
                canvas.DrawString(lines[i], rect.Left - rect.Width, top + i * lineHeight, rect.Width * 3f, lineHeight,
                    HorizontalAlignment.Center, VerticalAlignment.Center, TextFlow.OverflowBounds);
        }

        // The measure uses the plain system font on Android, narrower than Nunito that is drawn: a reserve of 15 % keeps the words inside.
        private static float Measure(ICanvas canvas, IFont font, float fs, string text) => canvas.GetStringSize(text, font, fs).Width * 1.15f;

        private static List<string>? Layout(ICanvas canvas, IFont font, float fs, float width, string text)
        {
            if (Measure(canvas, font, fs, text) <= width) return [text];
            var two = SplitInTwo(canvas, font, fs, text);
            if (two is null) return null;
            return two.All(l => Measure(canvas, font, fs, l) <= width) ? two : null;
        }

        // the split at a space that makes the longer of the two lines the shortest
        private static List<string>? SplitInTwo(ICanvas canvas, IFont font, float fs, string text)
        {
            List<string>? best = null;
            float bestWidth = float.MaxValue;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != ' ') continue;
                var a = text[..i].Trim();
                var b = text[(i + 1)..].Trim();
                if (a.Length == 0 || b.Length == 0) continue;
                float w = Math.Max(Measure(canvas, font, fs, a), Measure(canvas, font, fs, b));
                if (w < bestWidth) { bestWidth = w; best = [a, b]; }
            }
            return best;
        }
    }
}
