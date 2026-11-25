using Jumbo.Shared;

namespace Jumbo.Tests;

public class SharedHelpersTests
{
    [Theory]
    [InlineData("+998 90 123 45 67", "+998 90 123 45 67")]
    [InlineData("  +998-90-123-45-67 ", "+998901234567")]
    [InlineData("90 (123) 45 67", "90 123 45 67")]
    [InlineData("abc", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Phone_numbers_keep_only_plus_digits_and_spaces(string? input, string? expected)
        => Assert.Equal(expected, PhoneNumbers.Clean(input));

    [Fact]
    public void Phone_numbers_are_cut_at_15_digits()
        => Assert.Equal(15, PhoneNumbers.Clean("+1234567890123456789")!.Count(char.IsAsciiDigit));

    [Theory]
    [InlineData(1, "A0000001")]
    [InlineData(9_999_999, "A9999999")]
    [InlineData(10_000_000, "B0000000")]
    public void Public_id_is_a_letter_and_seven_digits(int userId, string text)
    {
        Assert.Equal(text, PublicId.Of(userId));
        Assert.Equal(userId, PublicId.ToUserId(text));
    }

    [Theory]
    [InlineData("a0000001", 1)]
    [InlineData(" A0000042 ", 42)]
    [InlineData("A000001", null)]
    [InlineData("1A000001", null)]
    [InlineData("A00000x1", null)]
    [InlineData(null, null)]
    public void Public_id_is_read_back_and_checked(string? text, int? userId)
        => Assert.Equal(userId, PublicId.ToUserId(text));

    [Fact]
    public void Public_id_rejects_ids_outside_the_range()
        => Assert.Throws<ArgumentOutOfRangeException>(() => PublicId.Of(-1));

    [Theory]
    [InlineData("Apple", "apple", 1, true)]
    [InlineData("  apple ", "Apple", 1, true)]
    [InlineData("aple", "apple", 1, false)]
    [InlineData("sut", "süt", 4, true)]          // Turkish: the letters a keyboard may lack are not required
    [InlineData("SÜT", "süt", 4, true)]
    [InlineData("sut", "süt", 1, false)]         // only for Turkish
    [InlineData("kus", "kuş", 4, true)]
    public void Typed_words_are_compared_by_language_rules(string entered, string word, int languageId, bool right)
        => Assert.Equal(right, WordMatch.TypedEquals(entered, word, languageId));

    [Fact]
    public void The_same_translation_ignores_case_and_spaces_but_never_matches_empty()
    {
        Assert.True(WordMatch.SameMeaning("Good  Morning", " good morning"));
        Assert.False(WordMatch.SameMeaning("", ""));
        Assert.False(WordMatch.SameMeaning(null, null));
    }

    [Theory]
    [InlineData("<p>Hello <b>world</b></p>")]
    [InlineData("<ul><li>one</li><li>two</li></ul>")]
    public void Safe_html_keeps_simple_markup(string html)
        => Assert.Equal(html, SafeHtml.Clean(html));

    [Theory]
    [InlineData("<script>alert(1)</script>Hi", "script")]
    [InlineData("<p onclick=\"x()\">Hi</p>", "onclick")]
    [InlineData("<a href=\"javascript:alert(1)\">go</a>", "javascript")]
    [InlineData("<img src=x onerror=alert(1)>", "onerror")]
    [InlineData("<iframe src=\"http://evil\"></iframe>text", "iframe")]
    public void Safe_html_removes_what_could_run_code(string html, string forbidden)
        => Assert.DoesNotContain(forbidden, SafeHtml.Clean(html), StringComparison.OrdinalIgnoreCase);

    [Fact]
    public void Safe_html_of_nothing_is_empty()
        => Assert.Equal(string.Empty, SafeHtml.Clean(null));

    [Theory]
    [InlineData(1, "en")]
    [InlineData(2, "ru")]
    [InlineData(3, "uz")]
    [InlineData(4, "tr")]
    public void Topic_video_language_codes_match_the_language_ids(int id, string code)
    {
        Assert.Equal(code, TopicVideoPaths.LanguageCode(id));
        Assert.True(TopicVideoPaths.IsLanguage(code));
    }

    [Fact]
    public void Money_is_grouped_with_spaces()
        => Assert.Equal("1 250 000", Money.Format(1_250_000));
}
