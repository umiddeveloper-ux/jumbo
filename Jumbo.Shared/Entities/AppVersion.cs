namespace Jumbo.Shared.Entities;

/// <summary>What the server says about the versions of the app on one platform (android | ios | windows): the apps read it to offer an update.</summary>
public class AppVersion
{
    public int AppVersionId { get; set; }
    public string Platform { get; set; } = null!;

    // The newest build that everybody can get from the store (the app offers "Update" when its own number is lower). 0 = nothing to offer.
    public int LatestCode { get; set; }

    // Builds below this number are not allowed to go on: they get a screen "Update" without a way to skip it. 0 = nobody is forced.
    public int MinCode { get; set; }

    // Where "Update" leads: the page of the app in the store, or a file.
    public string? Url { get; set; }

    // What is new, a line in each language (may be empty: the app has its own text then).
    public string? NotesRu { get; set; }
    public string? NotesEn { get; set; }
    public string? NotesUz { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
