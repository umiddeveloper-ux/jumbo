using System.Security.Cryptography;

namespace JumboWeb.Services;

/// <summary>
/// The avatar frames of the books and the award pictures: static files in wwwroot/frames (made by tools/gen-frames.mjs from design/frames). They never change
/// under the same address: the address carries a short version made of the files themselves (?v=...), the browser keeps them for a year
/// and a new set of files gets a new address.
/// </summary>
public sealed class FrameAssets
{
    public const int Books = 14;                       // frames exist for the books 1..14 (the edition has 10, the rest come later)

    public string Version { get; }

    public FrameAssets(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.WebRootPath ?? string.Empty, "frames");
        if (!Directory.Exists(dir)) { Version = "0"; return; }

        using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (var file in Directory.GetFiles(dir, "*.svg").OrderBy(f => f, StringComparer.Ordinal))
        {
            sha.AppendData(System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(file)));
            sha.AppendData(File.ReadAllBytes(file));
        }
        Version = Convert.ToHexString(sha.GetHashAndReset())[..10].ToLowerInvariant();
    }

    /// <summary>The frame of a book: "frame" (the whole ornament) or "ring" (the plain ring for small avatars). The books above 14 use the last one.</summary>
    public string Url(string kind, int book) => $"/frames/{kind}-{Math.Clamp(book, 1, Books):00}.svg?v={Version}";

    /// <summary>A file of the folder (e.g. the picture of an award the API names: /frames/award-03.svg) with the version.</summary>
    public string Versioned(string path) => $"{path}?v={Version}";
}
