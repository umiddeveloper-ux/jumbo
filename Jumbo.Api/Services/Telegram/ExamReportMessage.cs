using System.Net;
using System.Text;
using Jumbo.Shared.Dtos;

namespace Jumbo.Api.Services.Telegram;

// The text and the buttons of the bot's /examreport answer (the director's exam report of the school): a short summary, and buttons that open the
// page /r/<token>/exams. Pure functions, so they are easy to test.
public static class ExamReportMessage
{
    private const int MaxSilentNames = 5;

    public static string Build(SchoolExamReportDto r, string lang)
    {
        var period = TelegramTexts.Get(lang, "examreport_p_" + r.Period.Key);
        var sb = new StringBuilder(TelegramTexts.Get(lang, "examreport_title", WebUtility.HtmlEncode(r.SchoolName), period)).Append("\n\n");

        var t = r.Totals;
        if (t.Attempts == 0)
        {
            sb.Append(TelegramTexts.Get(lang, "examreport_none"));
            return sb.ToString();
        }

        sb.Append(TelegramTexts.Get(lang, "examreport_totals", t.ExamsHeld, t.Attempts)).Append('\n');
        sb.Append(TelegramTexts.Get(lang, "examreport_part", t.Participants, t.Pupils)).Append('\n');
        if (t.AvgPercent is { } avg) sb.Append(TelegramTexts.Get(lang, "examreport_avg", avg)).Append('\n');

        // the best and the weakest team among the teams that took exams (the weakest only when there is more than one such team)
        var scored = r.Teams.Where(x => x.Attempts > 0 && x.AvgPercent is not null).ToList();
        if (scored.Count > 0)
        {
            var best = scored.OrderByDescending(x => x.AvgPercent).ThenByDescending(x => x.Attempts).ThenBy(x => x.TeamName, StringComparer.CurrentCultureIgnoreCase).First();
            sb.Append('\n').Append(TelegramTexts.Get(lang, "examreport_best", WebUtility.HtmlEncode(best.TeamName), best.AvgPercent));
            if (scored.Count > 1)
            {
                var weak = scored.OrderBy(x => x.AvgPercent).ThenBy(x => x.Attempts).ThenBy(x => x.TeamName, StringComparer.CurrentCultureIgnoreCase).First();
                if (weak != best) sb.Append('\n').Append(TelegramTexts.Get(lang, "examreport_weak", WebUtility.HtmlEncode(weak.TeamName), weak.AvgPercent));
            }
        }

        var silent = r.Teams.Where(x => x.Pupils > 0 && x.Attempts == 0).Select(x => WebUtility.HtmlEncode(x.TeamName)).ToList();
        if (silent.Count > 0)
        {
            var names = string.Join(", ", silent.Take(MaxSilentNames)) + (silent.Count > MaxSilentNames ? $" +{silent.Count - MaxSilentNames}" : "");
            sb.Append("\n\n").Append(TelegramTexts.Get(lang, "examreport_silent", names));
        }
        return sb.ToString();
    }

    /// <summary>The inline buttons: the report of the period shown, and the other two periods.</summary>
    public static object Buttons(string lang, string siteUrl, string token, string shownPeriod)
    {
        string Url(string period) => $"{siteUrl.TrimEnd('/')}/r/{token}/exams?period={period}";
        var open = new { text = TelegramTexts.Get(lang, "examreport_btn"), url = Url(shownPeriod) };
        var others = new[] { "week", "month", "term" }
            .Where(p => p != shownPeriod)
            .Select(p => new { text = PeriodLabel(lang, p), url = Url(p) })
            .ToArray();
        return new { inline_keyboard = new object[] { new[] { open }, others } };
    }

    private static string PeriodLabel(string lang, string period) => period switch
    {
        "week" => TelegramTexts.Get(lang, "examreport_btn_week"),
        "term" => TelegramTexts.Get(lang, "examreport_btn_term"),
        _ => lang switch { "uz" => "Oy", "en" => "Month", _ => "Месяц" }
    };
}
