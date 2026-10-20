using SkiaSharp;

// Cuts the blue studio background out of the elephant picture and writes the web assets.
var dir = AppContext.BaseDirectory;
var root = Path.GetFullPath(Path.Combine(dir, "../../../"));
using var src = SKBitmap.Decode(Path.Combine(root, "src.webp"));
Console.WriteLine($"{src.Width}x{src.Height}");
var W = src.Width; var H = src.Height;

SKColor Px(int x, int y) => src.GetPixel(x, y);
Console.WriteLine($"corners: {Px(2, 2)} {Px(W - 3, 2)} {Px(2, H - 3)} {Px(W - 3, H - 3)} mid-left {Px(5, H / 2)}");

const double Tight = 34;   // surely background
// background colour = average of the 4 corners
var bg = new[] { Px(2, 2), Px(W - 3, 2), Px(2, H - 3), Px(W - 3, H - 3) };
double br = bg.Average(c => (double)c.Red), bgc = bg.Average(c => (double)c.Green), bb = bg.Average(c => (double)c.Blue);

double Dist(SKColor c) => Math.Sqrt(Math.Pow(c.Red - br, 2) + Math.Pow(c.Green - bgc, 2) + Math.Pow(c.Blue - bb, 2));

// samples of the figure
foreach (var (x, y, n) in new[] { (340, 600, "belly"), (150, 560, "arm"), (100, 330, "ear-in"), (340, 80, "cap"), (50, 300, "ear-edge"), (340, 900, "leg") })
    Console.WriteLine($"{n}: {Px(x, y)} dist={Dist(Px(x, y)):F0}");

// the floor shadow under the feet is a darker, still saturated blue: also background (only in the feet area, the cap is far away)
bool IsBgLike(int x, int y)
{
    var c = Px(x, y);
    if (Dist(c) < Tight) return true;
    if (y < H - 140) return false;
    c.ToHsv(out var h, out var sat, out var v);
    return h > 190 && h < 228 && sat > 45 && v > 55;
}

// 1. background region = pixels close to the background colour, reachable from the picture border (flood fill)
var isBg = new bool[W * H];
var stack = new Stack<int>();
void Seed(int x, int y) { var i = y * W + x; if (!isBg[i] && IsBgLike(x, y)) { isBg[i] = true; stack.Push(i); } }
for (var x = 0; x < W; x++) { Seed(x, 0); Seed(x, H - 1); }
for (var y = 0; y < H; y++) { Seed(0, y); Seed(W - 1, y); }
while (stack.Count > 0)
{
    var i = stack.Pop(); var x = i % W; var y = i / W;
    foreach (var (nx, ny) in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
        if (nx >= 0 && ny >= 0 && nx < W && ny < H) Seed(nx, ny);
}
// enclosed background pockets (between an arm and the body, ...): same colour, not reachable from the border
var pocketSeen = new bool[W * H];
for (var y = 0; y < H; y++) for (var x = 0; x < W; x++)
{
    var i = y * W + x;
    if (isBg[i] || pocketSeen[i] || !IsBgLike(x, y)) continue;
    var comp = new List<int>(); var st = new Stack<int>(); st.Push(i); pocketSeen[i] = true;
    while (st.Count > 0)
    {
        var j = st.Pop(); comp.Add(j); var cx = j % W; var cy = j / W;
        foreach (var (nx, ny) in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
            if (nx >= 0 && ny >= 0 && nx < W && ny < H && !pocketSeen[ny * W + nx] && !isBg[ny * W + nx] && IsBgLike(nx, ny)) { pocketSeen[ny * W + nx] = true; st.Push(ny * W + nx); }
    }
    if (comp.Count > 40) foreach (var j in comp) isBg[j] = true;   // a real pocket, not a speck inside the figure
}

// 2. alpha: background 0; the pixels next to it get a soft edge from their colour distance; the colour is un-mixed from the background
using var outBmp = new SKBitmap(W, H, SKColorType.Rgba8888, SKAlphaType.Unpremul);
bool NearBg(int x, int y, int r)
{
    for (var dy = -r; dy <= r; dy++) for (var dx = -r; dx <= r; dx++)
    {
        var nx = x + dx; var ny = y + dy;
        if (nx >= 0 && ny >= 0 && nx < W && ny < H && isBg[ny * W + nx]) return true;
    }
    return false;
}
for (var y = 0; y < H; y++) for (var x = 0; x < W; x++)
{
    var c = Px(x, y);
    if (isBg[y * W + x]) { outBmp.SetPixel(x, y, SKColors.Transparent); continue; }
    if (NearBg(x, y, 2))
    {
        var d = Dist(c);
        var a = Math.Clamp((d - Tight) / 38.0, 0, 1);
        a = Math.Max(a, 0.0);
        if (a <= 0.02) { outBmp.SetPixel(x, y, SKColors.Transparent); continue; }
        byte U(double p, double b) => (byte)Math.Clamp((p - (1 - a) * b) / a, 0, 255);
        outBmp.SetPixel(x, y, new SKColor(U(c.Red, br), U(c.Green, bgc), U(c.Blue, bb), (byte)(a * 255)));
    }
    else outBmp.SetPixel(x, y, new SKColor(c.Red, c.Green, c.Blue, 255));
}

// bounding box of the visible figure
int minX = W, minY = H, maxX = 0, maxY = 0;
for (var y = 0; y < H; y++) for (var x = 0; x < W; x++)
    if (outBmp.GetPixel(x, y).Alpha > 16) { minX = Math.Min(minX, x); maxX = Math.Max(maxX, x); minY = Math.Min(minY, y); maxY = Math.Max(maxY, y); }
Console.WriteLine($"figure bbox: {minX},{minY} - {maxX},{maxY}");

var outDir = Path.Combine(root, "out"); Directory.CreateDirectory(outDir);
SKBitmap Crop(int x0, int y0, int x1, int y1, int outW)
{
    var w = x1 - x0; var h = y1 - y0;
    var outH = (int)Math.Round(outW * (double)h / w);
    var bmp = new SKBitmap(outW, outH, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var cv = new SKCanvas(bmp);
    cv.Clear(SKColors.Transparent);
    using var paint = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
    using var img = SKImage.FromBitmap(outBmp);
    cv.DrawImage(img, new SKRect(x0, y0, x1, y1), new SKRect(0, 0, outW, outH), paint);
    return bmp;
}
void FadeBottom(SKBitmap b, float frac)
{
    using var cv = new SKCanvas(b);
    using var p = new SKPaint { BlendMode = SKBlendMode.DstIn, Shader = SKShader.CreateLinearGradient(new SKPoint(0, b.Height * (1 - frac)), new SKPoint(0, b.Height), new[] { SKColors.White, SKColors.Transparent }, SKShaderTileMode.Clamp) };
    cv.DrawRect(0, b.Height * (1 - frac), b.Width, b.Height * frac + 1, p);
}
void Save(SKBitmap b, string name, SKEncodedImageFormat f, int q)
{
    using var img = SKImage.FromBitmap(b); using var data = img.Encode(f, q);
    File.WriteAllBytes(Path.Combine(outDir, name), data.ToArray());
    Console.WriteLine($"{name}: {b.Width}x{b.Height}, {data.Size / 1024} KB");
}

var pad = 6;
using (var full = Crop(minX - pad, minY - pad, maxX + pad + 1, maxY + pad + 1, 420)) { Save(full, "jumbo.webp", SKEncodedImageFormat.Webp, 92); Save(full, "jumbo.png", SKEncodedImageFormat.Png, 100); }

// head: from the top of the figure to just under the chin (the picture's y ~ 470 of 1024)
var headBottom = minY + (int)((maxY - minY) * 0.49);
using (var head = Crop(minX - pad, minY - pad, maxX + pad + 1, headBottom, 360)) { FadeBottom(head, 0.2f); Save(head, "jumbo-head.webp", SKEncodedImageFormat.Webp, 92); Save(head, "jumbo-head.png", SKEncodedImageFormat.Png, 100); }
Console.WriteLine($"headBottom={headBottom}");

// a preview on a light and a dark checker to judge the cut
using var prev = new SKBitmap(1000, 700);
using (var cv = new SKCanvas(prev))
{
    cv.Clear(new SKColor(255, 255, 255));
    using var dark = new SKPaint { Color = new SKColor(20, 28, 48) };
    cv.DrawRect(500, 0, 500, 700, dark);
    using var img = SKImage.FromBitmap(outBmp);
    using var p = new SKPaint { FilterQuality = SKFilterQuality.High };
    cv.DrawImage(img, new SKRect(minX, minY, maxX, maxY), new SKRect(40, 20, 40 + 400, 20 + 400 * (maxY - minY) / (float)(maxX - minX) * 0.0f + 660), p);
    cv.DrawImage(img, new SKRect(minX, minY, maxX, maxY), new SKRect(540, 20, 940, 680), p);
}
Save(prev, "preview.png", SKEncodedImageFormat.Png, 100);

// ── app icons: the elephant's head on the brand-blue rounded square ──
SKBitmap Icon(int size)
{
    var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var cv = new SKCanvas(bmp);
    cv.Clear(SKColors.Transparent);
    using var bgPaint = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(size, size), new[] { new SKColor(0x3C, 0xC4, 0xFF), new SKColor(0x18, 0x99, 0xD6) }, SKShaderTileMode.Clamp) };
    cv.DrawRoundRect(new SKRect(0, 0, size, size), size * 0.22f, size * 0.22f, bgPaint);
    using var headSrc = Crop(minX - pad, minY - pad, maxX + pad + 1, headBottom, 720);
    FadeBottom(headSrc, 0.2f);
    using var img = SKImage.FromBitmap(headSrc);
    var w = size * 0.80f; var h = w * headSrc.Height / headSrc.Width;
    using var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
    cv.DrawImage(img, new SKRect((size - w) / 2, (size - h) / 2 + size * 0.03f, (size + w) / 2, (size + h) / 2 + size * 0.03f), p);
    return bmp;
}
foreach (var (n, s) in new[] { ("favicon.png", 64), ("apple-touch-icon.png", 180), ("jumbo-icon-512.png", 512) })
    using (var ic = Icon(s)) Save(ic, n, SKEncodedImageFormat.Png, 100);
using (var ic = Icon(128))
{
    using var img = SKImage.FromBitmap(ic); using var data = img.Encode(SKEncodedImageFormat.Png, 100);
    File.WriteAllText(Path.Combine(outDir, "favicon.svg"),
        "<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n<svg width=\"128\" height=\"128\" viewBox=\"0 0 128 128\" xmlns=\"http://www.w3.org/2000/svg\" xmlns:xlink=\"http://www.w3.org/1999/xlink\">\n  <title>Jumbo</title>\n  <image width=\"128\" height=\"128\" href=\"data:image/png;base64," + Convert.ToBase64String(data.ToArray()) + "\"/>\n</svg>\n");
}

// ── mobile app assets ──
var mobDir = Path.Combine(outDir, "mobile"); Directory.CreateDirectory(mobDir);
void SaveM(SKBitmap b, string name) { using var img = SKImage.FromBitmap(b); using var data = img.Encode(SKEncodedImageFormat.Png, 100); File.WriteAllBytes(Path.Combine(mobDir, name), data.ToArray()); Console.WriteLine($"mobile/{name}: {b.Width}x{b.Height}, {data.Size / 1024} KB"); }
using (var full = Crop(minX - pad, minY - pad, maxX + pad + 1, maxY + pad + 1, 420)) SaveM(full, "jumbo_full.png");
using (var head = Crop(minX - pad, minY - pad, maxX + pad + 1, headBottom, 360)) { FadeBottom(head, 0.2f); SaveM(head, "jumbo_head.png"); }
SKBitmap OnCanvas(int size, float widthFrac, bool background)
{
    var bmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
    using var cv = new SKCanvas(bmp);
    cv.Clear(SKColors.Transparent);
    if (background)
    {
        using var bgPaint = new SKPaint { IsAntialias = true, Shader = SKShader.CreateLinearGradient(new SKPoint(0, 0), new SKPoint(size, size), new[] { new SKColor(0x3C, 0xC4, 0xFF), new SKColor(0x18, 0x99, 0xD6) }, SKShaderTileMode.Clamp) };
        cv.DrawRect(new SKRect(0, 0, size, size), bgPaint);
    }
    else
    {
        using var headSrc = Crop(minX - pad, minY - pad, maxX + pad + 1, headBottom, 720);
        FadeBottom(headSrc, 0.2f);
        using var img = SKImage.FromBitmap(headSrc);
        var w = size * widthFrac; var h = w * headSrc.Height / headSrc.Width;
        using var p = new SKPaint { FilterQuality = SKFilterQuality.High, IsAntialias = true };
        cv.DrawImage(img, new SKRect((size - w) / 2, (size - h) / 2 + size * 0.03f, (size + w) / 2, (size + h) / 2 + size * 0.03f), p);
    }
    return bmp;
}
using (var bgIcon = OnCanvas(512, 1, true)) SaveM(bgIcon, "appicon.png");
using (var fgIcon = OnCanvas(512, 0.92f, false)) SaveM(fgIcon, "appiconfg.png");
using (var splash = OnCanvas(256, 0.95f, false)) SaveM(splash, "splash.png");
