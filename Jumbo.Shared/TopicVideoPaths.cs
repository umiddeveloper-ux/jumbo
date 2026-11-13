namespace Jumbo.Shared;

/// <summary>
/// Where the video of a topic lies and how it is asked for. A video belongs to a study language, a book and a topic:
/// <c>/videos/{lang}/book-NN/MM.mp4</c> (lang = en / ru / uz / tr, NN = the book 01.., MM = the number of the topic in the book 01..).
/// The files are not part of the site: they lie in the media folder of the server and nginx serves them.
/// </summary>
public static class TopicVideoPaths
{
    /// <summary>The study languages that can have videos (the codes of the Languages table).</summary>
    public static readonly IReadOnlyList<string> Languages = ["en", "ru", "uz", "tr"];

    /// <summary>The code of a language by its id in the Languages table (1 = English, 2 = Russian, 3 = Uzbek, 4 = Turkish); English when unknown.</summary>
    public static string LanguageCode(int languageId) => languageId switch { 2 => "ru", 3 => "uz", 4 => "tr", _ => "en" };

    public static bool IsLanguage(string? code) => code is not null && Languages.Contains(code);

    /// <summary>The path inside the folder of the videos: <c>en/book-01/05.mp4</c>.</summary>
    public static string RelativePath(string lang, int book, int sequence, string extension = "mp4") =>
        $"{lang}/book-{book:D2}/{sequence:D2}.{extension}";

    /// <summary>The address on the site: <c>/videos/en/book-01/05.mp4</c>.</summary>
    public static string Url(string lang, int book, int sequence, string extension = "mp4") =>
        "/videos/" + RelativePath(lang, book, sequence, extension);
}
