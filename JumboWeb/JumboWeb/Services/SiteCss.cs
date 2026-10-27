using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace JumboWeb.Services;

/// <summary>
/// All the style sheets of the site glued into ONE file (/site.css?v=hash): one request instead of a dozen that block the first paint.
/// The files stay where they are and are edited as before; the bundle is made from them when the site starts (so it is always in step) and
/// is kept in memory. The hash in the address makes the cache of the browser safe to keep for a year.
/// </summary>
public sealed class SiteCss
{
    // the order matters: it is the order of the cascade
    private static readonly string[] Files =
    [
        "tailwind.css", "compat.css", "fonts.css", "app.css", "jumbo-theme.css", "tiger.css", "home.css", "rating.css",
        "top10.css", "lang.css", "exam.css", "schools.css", "teacher.css", "report.css", "jumbo-app.css"
    ];

    public string Content { get; }
    public string Version { get; }

    public SiteCss(IWebHostEnvironment env)
    {
        var sb = new StringBuilder();
        foreach (var name in Files)
        {
            var file = env.WebRootFileProvider.GetFileInfo(name);
            if (!file.Exists) continue;
            using var reader = new StreamReader(file.CreateReadStream(), Encoding.UTF8);
            var css = reader.ReadToEnd();
            // @charset is only allowed first, a source map comment points to a file that is not served
            css = Regex.Replace(css, "^\\uFEFF?@charset\\s+\"[^\"]*\";", string.Empty, RegexOptions.IgnoreCase);
            css = Regex.Replace(css, "/\\*#\\s*sourceMappingURL=[^*]*\\*/", string.Empty);
            sb.Append("/* ").Append(name).Append(" */\n").Append(css).Append('\n');
        }
        Content = sb.ToString();
        Version = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Content)))[..12].ToLowerInvariant();
    }
}
