using SkiaSharp;
using Svg.Skia;

namespace Jumbo.Controls;

/// <summary>
/// A picture drawn from SVG text (the pictures of words, topics, awards and market items come from the server as SVG). It replaces a
/// WebView per picture: every WebView is a whole browser, a page with forty of them was slow and heavy. Here the SVG is turned into a
/// small bitmap once (a few milliseconds, kept in a cache) and shown by an ordinary Image. The text may be the bare svg or a page
/// around it (see SvgHtmlWrapper): the part from "&lt;svg" to the last "&lt;/svg&gt;" is used.
/// </summary>
public class SvgPicture : Image
{
    public static readonly BindableProperty SvgProperty = BindableProperty.Create(
        nameof(Svg), typeof(string), typeof(SvgPicture), string.Empty, propertyChanged: (b, _, n) => ((SvgPicture)b).Render((string?)n));

    /// <summary>The longer side of the bitmap in pixels (the picture is shown smaller, the extra pixels keep it sharp).</summary>
    public static readonly BindableProperty RenderSizeProperty = BindableProperty.Create(nameof(RenderSize), typeof(int), typeof(SvgPicture), 288);

    public string Svg { get => (string)GetValue(SvgProperty); set => SetValue(SvgProperty, value); }
    public int RenderSize { get => (int)GetValue(RenderSizeProperty); set => SetValue(RenderSizeProperty, value); }

    private static readonly Dictionary<string, byte[]> Cache = new();
    private static readonly SemaphoreSlim Gate = new(2);        // two pictures at a time: a list of forty does not take the CPU
    private const int CacheLimit = 400;

    public SvgPicture()
    {
        Aspect = Aspect.AspectFit;
        InputTransparent = true;
    }

    private async void Render(string? text)
    {
        Source = null;
        if (string.IsNullOrWhiteSpace(text)) return;
        int size = RenderSize;
        var key = size + ":" + text;
        try
        {
            byte[]? png;
            lock (Cache) Cache.TryGetValue(key, out png);
            if (png is null)
            {
                await Gate.WaitAsync();
                try { png = await Task.Run(() => ToPng(text, size)); }
                finally { Gate.Release(); }
                if (png is null) return;
                lock (Cache)
                {
                    if (Cache.Count >= CacheLimit) Cache.Clear();
                    Cache[key] = png;
                }
            }
            if (Svg == text) Source = ImageSource.FromStream(() => new MemoryStream(png));     // the text may have changed while we drew
        }
        catch { /* a picture is decoration: leave it empty */ }
    }

    internal static byte[]? ToPng(string text, int size)
    {
        int start = text.IndexOf("<svg", StringComparison.OrdinalIgnoreCase);
        int end = text.LastIndexOf("</svg>", StringComparison.OrdinalIgnoreCase);
        if (start < 0 || end < start) return null;
        var svgText = text.Substring(start, end + 6 - start);

        using var svg = new SKSvg();
        svg.FromSvg(svgText);
        var picture = svg.Picture;
        if (picture is null) return null;
        var bounds = picture.CullRect;
        if (bounds.Width <= 0 || bounds.Height <= 0) return null;

        float scale = size / Math.Max(bounds.Width, bounds.Height);
        int w = Math.Max(1, (int)Math.Round(bounds.Width * scale));
        int h = Math.Max(1, (int)Math.Round(bounds.Height * scale));
        using var bitmap = new SKBitmap(w, h);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(scale);
        canvas.Translate(-bounds.Left, -bounds.Top);
        canvas.DrawPicture(picture);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
