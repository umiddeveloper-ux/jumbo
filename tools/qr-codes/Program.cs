using System.Net.Http.Json;
using System.Text.RegularExpressions;
using QRCoder;
using SkiaSharp;
using ZXing;
using ZXing.Common;

// QR codes of jumbo.uz.
//   dotnet run -- <folder>                the site and the books (0 = alphabet and numbers):     <folder>/*.png
//   dotnet run -- --subjects <folder>     every topic of every book, a folder per book:          <folder>/Book-01/05-body.png ...
// Each code is read back and compared with its address.
const string Site = "https://jumbo.uz";
var reader = new BarcodeReaderGeneric { Options = new DecodingOptions { TryHarder = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
int bad = 0, total = 0;

void Make(string path, string url)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    using var gen = new QRCodeGenerator();
    using var data = gen.CreateQrCode(url, QRCodeGenerator.ECCLevel.Q);
    int modules = data.ModuleMatrix.Count;                         // includes the quiet zone already
    int ppm = Math.Max(12, 1100 / modules);
    var png = new PngByteQRCode(data).GetGraphic(ppm, [11, 30, 51, 255], [255, 255, 255, 255], true);
    File.WriteAllBytes(path, png);

    // read it back like a phone would
    using var bmp = SKBitmap.Decode(png);
    var pixels = bmp.Pixels.SelectMany(p => new byte[] { p.Red, p.Green, p.Blue, 255 }).ToArray();
    var result = reader.Decode(new RGBLuminanceSource(pixels, bmp.Width, bmp.Height, RGBLuminanceSource.BitmapFormat.RGBA32));
    if (result?.Text != url)   // the decoder is picky about some sizes: try the picture smaller, as a phone camera sees it
    {
        foreach (var w in new[] { 600, 400, 300 })
        {
            using var small = bmp.Resize(new SKImageInfo(w, w), SKFilterQuality.Medium);
            var px = small.Pixels.SelectMany(p => new byte[] { p.Red, p.Green, p.Blue, 255 }).ToArray();
            result = reader.Decode(new RGBLuminanceSource(px, w, w, RGBLuminanceSource.BitmapFormat.RGBA32));
            if (result?.Text == url) break;
        }
    }
    if (result?.Text != url)   // the code is a clean picture: read it as a "pure" barcode too
    {
        var pure = new BarcodeReaderGeneric { Options = new DecodingOptions { PureBarcode = true, PossibleFormats = [BarcodeFormat.QR_CODE] } };
        result = pure.Decode(new RGBLuminanceSource(pixels, bmp.Width, bmp.Height, RGBLuminanceSource.BitmapFormat.RGBA32));
        Console.WriteLine($"  pure read of {Path.GetFileName(path)}: {result?.Text}");
    }
    bool ok = result?.Text == url;
    total++;
    if (!ok) { bad++; Console.WriteLine($"BAD {path} -> {result?.Text}"); }
}

if (args.Contains("--subjects"))
{
    var outDir = args.Last(a => a != "--subjects");
    using var http = new HttpClient();
    // Book 0 has no topics: two codes, one for the alphabet and one for the numbers (the page scrolls to that part)
    Make(Path.Combine(outDir, "Book-00", "01-alphabet-abc.png"), $"{Site}/little-polyglot/book-zero#alphabet");
    Make(Path.Combine(outDir, "Book-00", "02-numbers-123.png"), $"{Site}/little-polyglot/book-zero#numbers");
    for (int book = 1; book <= 10; book++)
    {
        var subjects = await http.GetFromJsonAsync<List<SubjectDto>>($"{Site}/api/subjects/by-book/{book}") ?? [];
        foreach (var s in subjects.OrderBy(s => s.SequenceNumber))
        {
            var slug = Regex.Replace(s.SubjectName.ToLowerInvariant(), "[^a-z0-9]+", "-").Trim('-');
            Make(Path.Combine(outDir, $"Book-{book:D2}", $"{s.SequenceNumber:D2}-{slug}.png"), $"{Site}/topic/{book}/{s.SequenceNumber}");
        }
        Console.WriteLine($"book {book}: {subjects.Count} topics");
    }
}
else
{
    var outDir = args.Length > 0 ? args[0] : "out";
    Make(Path.Combine(outDir, "site-jumbo-uz.png"), Site + "/");
    Make(Path.Combine(outDir, "book-0-alphabet-abc-123.png"), Site + "/little-polyglot/book-zero");
    for (int b = 1; b <= 10; b++) Make(Path.Combine(outDir, $"book-{b:D2}.png"), $"{Site}/little-polyglot/book/{b}");
}
Console.WriteLine(bad == 0 ? $"all {total} codes read back correctly" : $"{bad} of {total} codes failed");

record SubjectDto(int SubjectId, int BookId, string SubjectName, int SequenceNumber);
