namespace JumboWeb.Services;

// Where the mobile apps can be downloaded from. Priority for Android:
//   1. Apps:AndroidUrl in configuration (e.g. a Google Play link),
//   2. downloads/jumbo.apk if the file is there (the folder "downloads" next to the media videos on the server, or wwwroot/downloads),
//   3. nothing yet -> the site shows "Coming soon".
// iOS: Apps:IosUrl (App Store link) or "coming soon".
public class AppDownloads
{
    public string? AndroidUrl { get; }
    public string? IosUrl { get; }
    public string? WindowsUrl { get; }
    public string? MacUrl { get; }

    public AppDownloads(IConfiguration config, IWebHostEnvironment env)
    {
        // Big files are kept outside the web image: <media>/downloads (nginx serves /downloads/ from there), <media> = the parent of Media:VideosRoot.
        // wwwroot/downloads still works (a small file inside the image).
        var folders = new List<string>();
        if (Blank(config["Media:VideosRoot"]) is { } videos && Path.GetDirectoryName(videos.TrimEnd('/', '\\')) is { Length: > 0 } media)
            folders.Add(Path.Combine(media, "downloads"));
        folders.Add(Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "downloads"));

        AndroidUrl = Blank(config["Apps:AndroidUrl"]) ?? FirstFile(folders, "jumbo.apk");
        IosUrl = Blank(config["Apps:IosUrl"]);

        // Windows / macOS: a link in the settings (Apps:WindowsUrl / Apps:MacUrl) or a file put in the downloads folder (the first that is there).
        WindowsUrl = Blank(config["Apps:WindowsUrl"]) ?? FirstFile(folders, "jumbo-windows.zip", "jumbo-windows.msix", "jumbo-setup.exe", "jumbo.msix");
        MacUrl = Blank(config["Apps:MacUrl"]) ?? FirstFile(folders, "jumbo-mac.dmg", "jumbo-macos.zip", "jumbo.dmg");
    }

    private static string? FirstFile(List<string> folders, params string[] names)
    {
        foreach (var n in names)
            foreach (var f in folders)
                if (File.Exists(Path.Combine(f, n))) return "/downloads/" + n;
        return null;
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
