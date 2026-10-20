namespace Jumbo.Shared;

/// <summary>
/// The identifier of a learner that people can read out and type, and that is used in payments: a letter A-Z and 7 digits, for example
/// <c>A0000001</c>. The digits are the learner's id in the database filled up with zeros to 7 places; when the id passes 9 999 999 the
/// letter goes on (B0000000 is the id 10 000 000). The database computes the same text itself (the stored column users.public_id,
/// see UserConfiguration): this class is for showing and for checking what somebody typed.
/// </summary>
public static class PublicId
{
    public const int Digits = 7;
    private const int PerLetter = 10_000_000;

    /// <summary>The identifier of the learner with this id in the database (id 1 gives A0000001).</summary>
    public static string Of(int userId)
    {
        if (userId < 0 || userId >= PerLetter * 26) throw new ArgumentOutOfRangeException(nameof(userId));
        return (char)('A' + userId / PerLetter) + (userId % PerLetter).ToString("D7");
    }

    /// <summary>The id in the database for an identifier, or null when the text is not one (letter A-Z + 7 digits; case does not matter).</summary>
    public static int? ToUserId(string? text)
    {
        var s = text?.Trim().ToUpperInvariant();
        if (s is not { Length: 8 } || s[0] is < 'A' or > 'Z' || s.AsSpan(1).ContainsAnyExceptInRange('0', '9')) return null;
        return (s[0] - 'A') * PerLetter + int.Parse(s.AsSpan(1));
    }

    public static bool IsValid(string? text) => ToUserId(text) is not null;
}
