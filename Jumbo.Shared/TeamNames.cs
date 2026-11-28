using System.Globalization;

namespace Jumbo.Shared;

/// <summary>
/// The order of the team names of a school ("3A", "3Б", "10A", "11A" ...): by the number at the start first (3 comes before 10, as the classes of a school do),
/// then by the rest of the name (A, Б, В; the case does not matter). A name without a number at the start ("А_2020") comes after the numbered ones.
/// A database sorts the names as text ("10A" before "3A"), so the lists are put in this order after they are read.
/// </summary>
public static class TeamNames
{
    public static readonly IComparer<string?> Natural = new NaturalComparer();

    private sealed class NaturalComparer : IComparer<string?>
    {
        private static readonly CompareInfo Culture = CultureInfo.InvariantCulture.CompareInfo;

        public int Compare(string? x, string? y)
        {
            var (nx, rx) = Split(x);
            var (ny, ry) = Split(y);
            if (nx is null && ny is not null) return 1;            // numbered names first
            if (nx is not null && ny is null) return -1;
            if (nx is not null && ny is not null && nx != ny) return nx.Value.CompareTo(ny.Value);
            var byText = Culture.Compare(rx, ry, CompareOptions.IgnoreCase);
            return byText != 0 ? byText : string.CompareOrdinal(x, y);
        }

        // "10А" -> (10, "А"); "А_2020" -> (null, "А_2020")
        private static (int? Number, string Tail) Split(string? name)
        {
            var s = (name ?? string.Empty).Trim();
            var i = 0;
            while (i < s.Length && i < 9 && char.IsAsciiDigit(s[i])) i++;
            return i == 0 ? (null, s) : (int.Parse(s.AsSpan(0, i)), s[i..].TrimStart(' ', '-', '_', '.'));
        }
    }
}
