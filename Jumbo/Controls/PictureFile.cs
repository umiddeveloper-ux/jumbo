using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Jumbo.Services;
using SkiaSharp;

namespace Jumbo.Controls;

/// <summary>
/// The picture files of the words (/pics/{name}.webp on the site). A file never changes (its name is made from its content), so it is
/// downloaded once and kept in the cache folder of the app: the next start, and a start without the network, show it from the disk.
/// </summary>
internal static partial class PictureFile
{
    [GeneratedRegex("^[0-9a-f]{16}\\.webp$")]
    private static partial Regex GoodName();

    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(25) };
    private static readonly ConcurrentDictionary<string, Task<string?>> Downloads = new();

    private static string Folder => Path.Combine(FileSystem.CacheDirectory, "pics");

    /// <summary>The path of the file on the disk (downloaded now when it is not there yet), or null when it could not be had.</summary>
    public static Task<string?> EnsureAsync(string address)
    {
        var name = Path.GetFileName(address);
        if (!GoodName().IsMatch(name)) return Task.FromResult<string?>(null);       // only what the server really makes
        var path = Path.Combine(Folder, name);
        if (File.Exists(path)) return Task.FromResult<string?>(path);

        // one download per file, however many pictures ask for it at the same moment
        var task = Downloads.GetOrAdd(name, _ => DownloadAsync(address, path));
        _ = task.ContinueWith(_ => Downloads.TryRemove(name, out Task<string?>? _), TaskScheduler.Default);
        return task;
    }

    private static async Task<string?> DownloadAsync(string address, string path)
    {
        var url = ApiConfig.PicsBaseUrl + address.TrimStart('/');
        for (var attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var response = await Http.GetAsync(url);
                if (!response.IsSuccessStatusCode) return null;
                var bytes = await response.Content.ReadAsByteArrayAsync();
                if (bytes.Length == 0) return null;
                Directory.CreateDirectory(Folder);
                var temp = path + "." + Guid.NewGuid().ToString("N")[..8] + ".tmp";
                await File.WriteAllBytesAsync(temp, bytes);
                File.Move(temp, path, overwrite: true);                             // never a half-written file under the real name
                return path;
            }
            catch (Exception) when (attempt == 0)
            {
                await Task.Delay(1200);
            }
            catch (Exception)
            {
                return null;
            }
        }
        return null;
    }

    /// <summary>
    /// The file as a square PNG of <paramref name="size"/> pixels with the rounded corners the svg pictures had (a square with the corner radius of
    /// 18 % of its side), so a picture looks the same whether it comes from a file or from svg text.
    /// </summary>
    public static byte[]? ToPng(string path, int size)
    {
        using var source = SKBitmap.Decode(path);
        if (source is null || source.Width <= 0 || source.Height <= 0) return null;

        // the middle square of the photo (the files are square already; this keeps any other shape from being squeezed)
        int side = Math.Min(source.Width, source.Height);
        var from = new SKRect((source.Width - side) / 2f, (source.Height - side) / 2f, (source.Width + side) / 2f, (source.Height + side) / 2f);

        using var bitmap = new SKBitmap(size, size);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        var box = new SKRect(0, 0, size, size);
        using var rounded = new SKRoundRect(box, size * 0.18f, size * 0.18f);
        canvas.ClipRoundRect(rounded, SKClipOperation.Intersect, antialias: true);
        using var photo = SKImage.FromBitmap(source);
        using var paint = new SKPaint { IsAntialias = true };
        canvas.DrawImage(photo, from, box, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.Linear), paint);
        canvas.Flush();
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 90);
        return data.ToArray();
    }
}
