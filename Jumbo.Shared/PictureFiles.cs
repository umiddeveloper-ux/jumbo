using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Jumbo.Shared;

/// <summary>
/// The pictures of the words are photos wrapped in a small svg (a picture inside a rounded square). They are also made as files:
/// /pics/{name}.webp, the name being the first 16 hex letters of the SHA-256 of the photo (the bytes inside the svg) - so the name does
/// not depend on the database (local and production numbers differ) and the same photo is one file. The files are made by
/// tools/build-pictures.mjs, which uses the same rule; the API tells the clients the address (ImageUrl) when the name is known.
/// </summary>
public static partial class PictureFiles
{
    public const string UrlPrefix = "/pics/";

    [GeneratedRegex(@"data:image/(?:jpeg|png|webp);base64,([A-Za-z0-9+/=]+)")]
    private static partial Regex Embedded();

    /// <summary>The file name for the photo inside an svg, or null when the svg has none (a drawing) or it cannot be read.</summary>
    public static string? FileNameOf(string? svg)
    {
        if (string.IsNullOrEmpty(svg)) return null;
        var m = Embedded().Match(svg);
        if (!m.Success) return null;
        try
        {
            var bytes = Convert.FromBase64String(m.Groups[1].Value);
            return Convert.ToHexString(SHA256.HashData(bytes))[..16].ToLowerInvariant() + ".webp";
        }
        catch (FormatException) { return null; }
    }

    /// <summary>
    /// A small svg for the website that shows a picture file in the same rounded square as the old svg (the same box, so every style that
    /// sizes the old svg works as before).
    /// </summary>
    public static string SvgFor(string imageUrl) =>
        "<svg viewBox='0 0 100 100' xmlns='http://www.w3.org/2000/svg'><defs><clipPath id='c'><rect width='100' height='100' rx='18'/></clipPath></defs>" +
        $"<g clip-path='url(#c)'><image href='{System.Net.WebUtility.HtmlEncode(imageUrl)}' width='100' height='100' preserveAspectRatio='xMidYMid slice'/></g></svg>";

    /// <summary>The address of a picture file for the clients (relative to the site), or null when there is no file.</summary>
    public static string? UrlOf(string? fileName) => string.IsNullOrEmpty(fileName) ? null : UrlPrefix + fileName;
}
