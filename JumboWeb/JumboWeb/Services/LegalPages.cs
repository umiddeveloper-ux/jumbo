using System.Globalization;
using System.Net;
using System.Text;
using Microsoft.Extensions.Localization;

namespace JumboWeb.Services;

/// <summary>
/// The privacy policy (/privacy) and the "how to delete the account" page (/delete-account): open to everybody, plain HTML made on the
/// server (no script is needed to read them, Google Play and search robots see the text). The language is the visitor's own
/// (the site's cookie / country / browser) or ?lang=ru|en|uz.
/// </summary>
public static class LegalPages
{
    public static void MapLegalPages(this WebApplication app)
    {
        app.MapGet("/privacy", (HttpContext ctx, IStringLocalizer<AppResources> loc, IConfiguration cfg) => Render(ctx, loc, cfg, privacy: true));
        app.MapGet("/delete-account", (HttpContext ctx, IStringLocalizer<AppResources> loc, IConfiguration cfg) => Render(ctx, loc, cfg, privacy: false));
    }

    private static IResult Render(HttpContext ctx, IStringLocalizer<AppResources> loc, IConfiguration cfg, bool privacy)
    {
        var lang = ctx.Request.Query["lang"].ToString() is "ru" or "en" or "uz" ? ctx.Request.Query["lang"].ToString() : AppLanguages.Resolve(ctx);
        string T(string key)
        {
            var saved = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(lang);
                var r = loc[key];
                return r.ResourceNotFound ? key : r.Value;
            }
            finally { CultureInfo.CurrentUICulture = saved; }
        }
        string E(string s) => WebUtility.HtmlEncode(s);

        var path = privacy ? "/privacy" : "/delete-account";
        var contact = cfg["Legal:ContactEmail"];
        var sb = new StringBuilder();

        // plain text of a block: "• " and "1. " lines are list items, the others paragraphs
        void Text(string text)
        {
            foreach (var line in text.Split('\n', StringSplitOptions.RemoveEmptyEntries))
                sb.Append(line.StartsWith("• ") || (line.Length > 2 && char.IsDigit(line[0]) && line[1] == '.') ? "<p class=\"item\">" : "<p>").Append(E(line)).Append("</p>");
        }

        // open to everybody (Google Play needs the link), but out of the search results: the landing page must be what people find
        ctx.Response.Headers["X-Robots-Tag"] = "noindex, follow";

        sb.Append("<!doctype html><html lang=\"").Append(lang).Append("\"><head><meta charset=\"utf-8\">")
          .Append("<meta name=\"robots\" content=\"noindex, follow\">")
          .Append("<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">")
          .Append("<title>").Append(E(T(privacy ? "Privacy_Title" : "Del_Title"))).Append(" — Jumbo.uz</title>")
          .Append("<style>")
          .Append(":root{color-scheme:light dark}body{margin:0 auto;max-width:720px;padding:16px 16px 48px;font:16px/1.55 system-ui,-apple-system,'Segoe UI',Roboto,sans-serif;background:#f5f9fc;color:#1f3a5f}")
          .Append("a{color:#1899d6}h1{font-size:1.6rem;margin:18px 0 4px}h2{font-size:1.1rem;margin:24px 0 6px}p{margin:0 0 8px}.item{padding-left:4px}")
          .Append(".top{display:flex;justify-content:space-between;align-items:center;gap:10px}.top a{text-decoration:none;font-weight:700}")
          .Append(".lang a{margin-left:10px;color:inherit;opacity:.6}.lang a.on{opacity:1;text-decoration:underline}.muted{opacity:.65;font-size:.85rem}")
          .Append(".btn{display:block;text-align:center;background:#58cc02;color:#fff;text-decoration:none;font-weight:800;padding:13px;border-radius:14px;margin:14px 0}")
          .Append("@media(prefers-color-scheme:dark){body{background:#0e1a1a;color:#e6f0f0}a{color:#4dd0c4}}")
          .Append("</style></head><body>");

        sb.Append("<div class=\"top\"><a href=\"/\">").Append(E(T("Tq_GoSite"))).Append(" →</a><span class=\"lang\">");
        foreach (var code in new[] { "ru", "en", "uz" })
            sb.Append("<a href=\"").Append(path).Append("?lang=").Append(code).Append("\"").Append(code == lang ? " class=\"on\"" : "").Append(">").Append(code.ToUpperInvariant()).Append("</a>");
        sb.Append("</span></div>");

        if (privacy)
        {
            sb.Append("<h1>").Append(E(T("Privacy_Title"))).Append("</h1><p class=\"muted\">").Append(E(T("Privacy_Updated"))).Append("</p>");
            for (int i = 1; i <= 8; i++)
            {
                sb.Append("<h2>").Append(E(T($"Privacy_H{i}"))).Append("</h2>");
                Text(T($"Privacy_P{i}"));
            }
            if (!string.IsNullOrWhiteSpace(contact))
                sb.Append("<p><strong>").Append(E(string.Format(T("Privacy_Contact"), contact))).Append("</strong></p>");
            sb.Append("<p><a href=\"/delete-account?lang=").Append(lang).Append("\">").Append(E(T("Del_Title"))).Append("</a></p>");
        }
        else
        {
            sb.Append("<h1>").Append(E(T("Del_Title"))).Append("</h1><p>").Append(E(T("Del_Intro"))).Append("</p>");
            sb.Append("<h2>").Append(E(T("Del_StepsTitle"))).Append("</h2>");
            Text(T("Del_Steps"));
            sb.Append("<a class=\"btn\" href=\"/little-polyglot/settings\">").Append(E(T("Del_Login"))).Append("</a>");
            sb.Append("<h2>").Append(E(T("Del_WhatTitle"))).Append("</h2>");
            Text(T("Del_What"));
            sb.Append("<p>").Append(E(T("Del_Staff"))).Append("</p>");
            if (!string.IsNullOrWhiteSpace(contact))
                sb.Append("<p><strong>").Append(E(string.Format(T("Privacy_Contact"), contact))).Append("</strong></p>");
            sb.Append("<p><a href=\"/privacy?lang=").Append(lang).Append("\">").Append(E(T("Privacy_Title"))).Append("</a></p>");
        }
        sb.Append("</body></html>");

        ctx.Response.Headers.CacheControl = "public, max-age=300";
        return Results.Content(sb.ToString(), "text/html; charset=utf-8");
    }
}
