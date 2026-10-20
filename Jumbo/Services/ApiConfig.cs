namespace Jumbo.Services;

public static class ApiConfig
{
    // The deployed site: the API lives under https://jumbo.uz/api (TLS, same address as the website).
    // For local development set the JUMBO_API environment variable (e.g. http://localhost:5231/) before launching.
    // The videos of the topics: <site>/videos/{lang}/book-NN/MM.mp4 (lang = the study language, NN = book, MM = topic number). A test build can send them elsewhere with JUMBO_VIDEO.
    public static string VideoBaseUrl
    {
        get
        {
#if DEBUG
            var v = Environment.GetEnvironmentVariable("JUMBO_VIDEO");
            if (!string.IsNullOrWhiteSpace(v)) return v.EndsWith('/') ? v : v + "/";
#endif
            return BaseUrl;
        }
    }

    public static string BaseUrl
    {
        get
        {
#if DEBUG
            var overrideUrl = Environment.GetEnvironmentVariable("JUMBO_API");
            if (!string.IsNullOrWhiteSpace(overrideUrl))
                return overrideUrl.EndsWith('/') ? overrideUrl : overrideUrl + "/";
#endif
            return "https://jumbo.uz/";
        }
    }
}
