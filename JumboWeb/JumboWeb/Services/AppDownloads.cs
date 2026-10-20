namespace JumboWeb.Services;

// Where the mobile apps can be downloaded from. Priority for Android:
//   1. Apps:AndroidUrl in configuration (e.g. a Google Play link),
//   2. wwwroot/downloads/jumbo.apk if the file is there,
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
        AndroidUrl = Blank(config["Apps:AndroidUrl"]);
        if (AndroidUrl is null)
        {
            var apk = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), "downloads", "jumbo.apk");
            if (File.Exists(apk)) AndroidUrl = "/downloads/jumbo.apk";
        }
        IosUrl = Blank(config["Apps:IosUrl"]);

        // Windows / macOS: a link in the settings (Apps:WindowsUrl / Apps:MacUrl) or a file put in wwwroot/downloads (the first that is there).
        var root = env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot");
        WindowsUrl = Blank(config["Apps:WindowsUrl"]) ?? FirstFile(root, "jumbo-windows.zip", "jumbo-windows.msix", "jumbo-setup.exe", "jumbo.msix");
        MacUrl = Blank(config["Apps:MacUrl"]) ?? FirstFile(root, "jumbo-mac.dmg", "jumbo-macos.zip", "jumbo.dmg");
    }

    private static string? FirstFile(string root, params string[] names)
    {
        foreach (var n in names)
            if (File.Exists(Path.Combine(root, "downloads", n))) return "/downloads/" + n;
        return null;
    }

    private static string? Blank(string? v) => string.IsNullOrWhiteSpace(v) ? null : v.Trim();
}
