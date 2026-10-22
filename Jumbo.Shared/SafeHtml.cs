using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace Jumbo.Shared;

/// <summary>
/// Makes the html of an ad safe to show on the site and in the apps: only a few simple tags stay (paragraphs, bold, italic, underline,
/// line breaks, lists, small headings and links to http(s) addresses), without any attributes except the address of a link. Scripts,
/// styles, event handlers, forms, frames and the like are removed (their text is kept as plain text, except script and style, which go
/// completely). The apps show this html in a label, which understands exactly this kind of simple markup.
/// </summary>
public static partial class SafeHtml
{
    private static readonly HashSet<string> Allowed =
        ["p", "br", "b", "strong", "i", "em", "u", "ul", "ol", "li", "h3", "h4", "a", "div", "span"];

    // the tag and everything up to its end for tags whose content must disappear too
    [GeneratedRegex(@"<\s*(script|style|iframe|object|embed|noscript|template)\b[\s\S]*?<\s*/\s*\1\s*>", RegexOptions.IgnoreCase)]
    private static partial Regex Dangerous();

    [GeneratedRegex(@"<!--[\s\S]*?-->")]
    private static partial Regex Comments();

    [GeneratedRegex(@"<\s*(/?)\s*([a-zA-Z][a-zA-Z0-9]*)((?:[^>""']|""[^""]*""|'[^']*')*)>")]
    private static partial Regex Tag();

    [GeneratedRegex(@"\bhref\s*=\s*(?:""([^""]*)""|'([^']*)'|([^\s>]+))", RegexOptions.IgnoreCase)]
    private static partial Regex Href();

    public const int MaxLength = 8000;

    public static string Clean(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        if (html.Length > MaxLength * 2) html = html[..(MaxLength * 2)];

        var s = Comments().Replace(Dangerous().Replace(html, string.Empty), string.Empty);

        var sb = new StringBuilder();
        var open = new Dictionary<string, int>();           // the tags that were written and are not closed yet: a closing tag of one that was dropped is dropped too
        int pos = 0;
        foreach (Match m in Tag().Matches(s))
        {
            sb.Append(Text(s.AsSpan(pos, m.Index - pos)));
            pos = m.Index + m.Length;

            bool closing = m.Groups[1].Length > 0;
            var name = m.Groups[2].Value.ToLowerInvariant();
            if (!Allowed.Contains(name)) continue;                    // the tag goes, its text stays

            if (closing)
            {
                if (name != "br" && open.GetValueOrDefault(name) > 0) { open[name]--; sb.Append("</").Append(name).Append('>'); }
                continue;
            }
            if (name == "a")
            {
                var href = HrefOf(m.Groups[3].Value);
                if (href is null) continue;                           // a link without a good address is just text
                sb.Append("<a href=\"").Append(WebUtility.HtmlEncode(href)).Append("\">");
                open["a"] = open.GetValueOrDefault("a") + 1;
            }
            else
            {
                sb.Append('<').Append(name).Append(name == "br" ? " />" : ">");
                if (name != "br") open[name] = open.GetValueOrDefault(name) + 1;
            }
        }
        sb.Append(Text(s.AsSpan(pos)));

        var result = sb.ToString().Trim();
        return result.Length > MaxLength ? result[..MaxLength] : result;
    }

    // a link must go to http(s) (or be an address of this site)
    private static string? HrefOf(string attributes)
    {
        var m = Href().Match(attributes);
        if (!m.Success) return null;
        var value = WebUtility.HtmlDecode((m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Success ? m.Groups[2].Value : m.Groups[3].Value).Trim());
        // control characters hide "javascript:" in some browsers: they are dropped before the check
        value = new string(value.Where(c => c >= ' ').ToArray());
        if (value.StartsWith("https://", StringComparison.OrdinalIgnoreCase) || value.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || (value.StartsWith('/') && !value.StartsWith("//")))
            return value;
        return null;
    }

    // plain text between the tags: a "<" that did not make a tag must not become one
    private static string Text(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty) return string.Empty;
        return text.ToString().Replace("<", "&lt;").Replace(">", "&gt;");
    }
}
