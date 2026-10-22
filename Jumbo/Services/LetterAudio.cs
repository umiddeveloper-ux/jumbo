namespace Jumbo.Services;

/// <summary>
/// The recording of a letter of the alphabet (Resources/Raw/alphabet/a.mp3 ... z.mp3, short clear recordings). Returns false when there is no
/// recording or the platform has no player for it: the caller then lets the voice say the letter, as before.
/// </summary>
public static class LetterAudio
{
    /// <param name="key">The key of a tile of Book 0: "LA" is the letter A, "N7" is a number (no recording: false).</param>
    public static async Task<bool> PlayAsync(string key, CancellationToken token)
    {
        if (key.Length != 2 || key[0] != 'L' || key[1] < 'A' || key[1] > 'Z') return false;
        try
        {
            var path = await EnsureFileAsync(char.ToLowerInvariant(key[1]));
            if (path is null) return false;
            return await PlatformPlayAsync(path, token);
        }
        catch (OperationCanceledException) { throw; }
        catch { return false; }      // the recording is a bonus: never break the page
    }

    // The player needs a real file, an asset of the package is not one: it is copied once into the cache (about 3 KB each).
    private static async Task<string?> EnsureFileAsync(char letter)
    {
        var dir = Path.Combine(FileSystem.CacheDirectory, "alphabet");
        var target = Path.Combine(dir, letter + ".mp3");
        if (File.Exists(target) && new FileInfo(target).Length > 0) return target;
        try
        {
            Directory.CreateDirectory(dir);
            await using var source = await FileSystem.OpenAppPackageFileAsync($"alphabet/{letter}.mp3");
            await using var file = File.Create(target);
            await source.CopyToAsync(file);
            return target;
        }
        catch { return null; }
    }

#if ANDROID
    private static async Task<bool> PlatformPlayAsync(string path, CancellationToken token)
    {
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var player = new Android.Media.MediaPlayer();
        player.Completion += (_, _) => done.TrySetResult(true);
        player.Error += (_, _) => done.TrySetResult(false);
        player.SetDataSource(path);
        player.Prepare();
        using var _ = token.Register(() => done.TrySetCanceled());
        player.Start();
        try { return await done.Task; }
        finally { try { if (player.IsPlaying) player.Stop(); } catch { } }
    }
#elif IOS || MACCATALYST
    private static async Task<bool> PlatformPlayAsync(string path, CancellationToken token)
    {
        var player = AVFoundation.AVAudioPlayer.FromUrl(Foundation.NSUrl.FromFilename(path), out var error);
        if (player is null || error is not null) return false;
        var done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        player.FinishedPlaying += (_, e) => done.TrySetResult(e.Status);
        using var _ = token.Register(() => done.TrySetCanceled());
        if (!player.Play()) return false;
        try { return await done.Task; }
        finally { player.Stop(); player.Dispose(); }
    }
#else
    // Windows: the voice says the letter as before
    private static Task<bool> PlatformPlayAsync(string path, CancellationToken token) => Task.FromResult(false);
#endif
}
