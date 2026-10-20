using Jumbo.Shared;

namespace JumboWeb.Services;

/// <summary>
/// The folder with the topic videos (<c>Media:VideosRoot</c>, on the server /media/videos, the same folder nginx serves at /videos/):
/// what is there, saving an uploaded file, deleting one. Only the admin page uses it.
/// </summary>
public class TopicVideoStore(IConfiguration configuration)
{
    /// <summary>The biggest file that can be uploaded.</summary>
    public const long MaxBytes = 500L * 1024 * 1024;

    public record VideoInfo(long Size, DateTime ModifiedUtc);

    public string Root => configuration["Media:VideosRoot"] is { Length: > 0 } root ? root : "/media/videos";

    private string PathOf(string lang, int book, int sequence)
    {
        if (!TopicVideoPaths.IsLanguage(lang) || book < 1 || book > Course.BookCount || sequence < 1 || sequence > 99)
            throw new ArgumentException("BAD_TOPIC");
        return Path.Combine(Root, lang, $"book-{book:D2}", $"{sequence:D2}.mp4");
    }

    public VideoInfo? Info(string lang, int book, int sequence)
    {
        var file = new FileInfo(PathOf(lang, book, sequence));
        return file.Exists ? new VideoInfo(file.Length, file.LastWriteTimeUtc) : null;
    }

    /// <summary>The videos of one book in one language, by the number of the topic.</summary>
    public Dictionary<int, VideoInfo> Book(string lang, int book)
    {
        var result = new Dictionary<int, VideoInfo>();
        if (!TopicVideoPaths.IsLanguage(lang) || book < 1 || book > Course.BookCount) return result;
        var dir = new DirectoryInfo(Path.Combine(Root, lang, $"book-{book:D2}"));
        if (!dir.Exists) return result;
        foreach (var f in dir.EnumerateFiles("*.mp4"))
            if (int.TryParse(Path.GetFileNameWithoutExtension(f.Name), out var n)) result[n] = new VideoInfo(f.Length, f.LastWriteTimeUtc);
        return result;
    }

    /// <summary>
    /// Saves the uploaded file as the video of the topic (a video that is there is replaced). It goes to a temporary file first, so a broken
    /// upload never leaves half a video under the real name. Throws InvalidDataException("NOT_MP4") / ("TOO_BIG").
    /// </summary>
    public async Task SaveAsync(string lang, int book, int sequence, Stream source, Action<long>? progress = null, CancellationToken ct = default)
    {
        var target = PathOf(lang, book, sequence);
        EnsureFolder(Path.GetDirectoryName(target)!);
        var temp = target + ".upload-" + Guid.NewGuid().ToString("N");

        try
        {
            await using (var output = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, useAsync: true))
            {
                var buffer = new byte[81920];
                long total = 0, lastReport = 0;
                bool checkedHead = false;
                int read;
                while ((read = await source.ReadAsync(buffer, ct)) > 0)
                {
                    if (!checkedHead)
                    {
                        // an mp4 starts with a box: 4 bytes of its size and then "ftyp"
                        if (read < 12 || buffer[4] != 'f' || buffer[5] != 't' || buffer[6] != 'y' || buffer[7] != 'p')
                            throw new InvalidDataException("NOT_MP4");
                        checkedHead = true;
                    }
                    total += read;
                    if (total > MaxBytes) throw new InvalidDataException("TOO_BIG");
                    await output.WriteAsync(buffer.AsMemory(0, read), ct);
                    if (progress is not null && total - lastReport >= 1024 * 1024) { lastReport = total; progress(total); }
                }
                if (!checkedHead) throw new InvalidDataException("NOT_MP4");
                progress?.Invoke(total);
            }
            File.Move(temp, target, overwrite: true);
            Open(target);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    // (Linux only: on the server the folder is shared with nginx and with the person who copies files there by hand.)
    private void EnsureFolder(string path)
    {
        var missing = new Stack<string>();
        for (var d = path; !string.IsNullOrEmpty(d) && !Directory.Exists(d) && d.Length > Root.Length; d = Path.GetDirectoryName(d)) missing.Push(d);
        Directory.CreateDirectory(path);
        foreach (var d in missing) Open(d);
    }

    private static void Open(string path)
    {
        if (OperatingSystem.IsWindows()) return;
        try
        {
            File.SetUnixFileMode(path, Directory.Exists(path)
                ? UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherWrite | UnixFileMode.OtherExecute
                : UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.GroupRead | UnixFileMode.GroupWrite | UnixFileMode.OtherRead | UnixFileMode.OtherWrite);
        }
        catch { /* a mode that cannot be set is not a reason to lose the upload */ }
    }

    public bool Delete(string lang, int book, int sequence)
    {
        var target = PathOf(lang, book, sequence);
        if (!File.Exists(target)) return false;
        File.Delete(target);
        return true;
    }
}
