using Jumbo.ApiClient;
using JumboWeb.Components;
using JumboWeb.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// ── Blazor: Interactive Server + WASM ───────────────────────────────────────
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddInteractiveWebAssemblyComponents();

// The keys that protect antiforgery tokens and other protected data must survive a restart of the container, otherwise
// every deploy breaks the pages that are open in people's browsers. Production points this at a Docker volume.
var keysPath = builder.Configuration["DataProtection:KeysPath"];
if (!string.IsNullOrWhiteSpace(keysPath))
    // The application name is the name the site had before it was renamed to Jumbo: it is part of what the login cookies are protected with,
    // so a new name would sign every user out. Keep it.
    builder.Services.AddDataProtection().SetApplicationName("LittlePolyglotWeb").PersistKeysToFileSystem(new DirectoryInfo(keysPath));

builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ── API client (all data access now goes through the Jumbo.Api backend) ──
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<AppDownloads>();
builder.Services.AddSingleton<SiteCss>();
builder.Services.AddSingleton<TopicVideoStore>();
builder.Services.AddScoped<SessionTokenAccessor>();
builder.Services.AddScoped<CircuitHandler, CookieCircuitHandler>();
var apiBaseUrl = builder.Configuration["Api:BaseUrl"] ?? "http://localhost:5000/";
builder.Services.AddHttpClient("Api", client => client.BaseAddress = new Uri(apiBaseUrl));

// LpApiClient is constructed manually (not via AddHttpClient<LpApiClient>) because the
// configure callback passed to AddHttpClient runs against HttpClientFactory's own internal
// provider, which cannot resolve Scoped services like SessionTokenAccessor (it throws
// "Cannot resolve scoped service from root provider"). A plain AddScoped factory delegate,
// by contrast, always receives the real calling/circuit scope, so the token can be read here.
builder.Services.AddScoped<LpApiClient>(sp =>
{
    var client = sp.GetRequiredService<IHttpClientFactory>().CreateClient("Api");
    var tokenAccessor = sp.GetRequiredService<SessionTokenAccessor>();
    if (!string.IsNullOrEmpty(tokenAccessor.Token))
        client.DefaultRequestHeaders.Add("X-Access-Token", tokenAccessor.Token);
    return new LpApiClient(client);
});

// ── Сервисы (Scoped — one instance per user circuit, not shared across users) ──
builder.Services.AddScoped<LivesService>();
builder.Services.AddScoped<UserStateService>();
builder.Services.AddScoped<UserPreferencesService>();
builder.Services.AddScoped<AppLocalizer>();
builder.Services.AddScoped<ExamStateService>();
builder.Services.AddScoped<BookGateService>();
builder.Services.AddScoped<PlayerStatsService>();
builder.Services.AddScoped<TestLevelStateService>();

var app = builder.Build();

// ── Completes registration: sets the access-token cookie then redirects home. ──
// A real HTTP GET (triggered via NavigationManager.NavigateTo(..., forceLoad: true)),
// not circuit code, so Response.Cookies is actually available here.
app.MapGet("/session/start", (HttpContext context, string token, bool remember = true, string? next = null) =>
{
    context.Response.Cookies.Append("access_token", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Expires = remember ? DateTimeOffset.MaxValue : null,
        Secure = context.Request.IsHttps,
        IsEssential = true
    });
    context.Response.Headers.CacheControl = "no-store";
    // Only same-site paths ("/join/ABC"), never "//host" or "/\host": no open redirect.
    var safeNext = next is { Length: > 1 } && next[0] == '/' && next[1] != '/' && next[1] != '\\' ? next : "/";
    return Results.Redirect(safeNext);
});

// ── "Open on the website signed in" from the mobile app: /go/{one-time code}?next=/rating ──
// The app gets a one-time code from the API; the token itself never appears in a URL.
app.MapGet("/go/{code:guid}", async (HttpContext context, Guid code, string? next, IHttpClientFactory httpFactory) =>
{
    string? token = null;
    try { token = await new LpApiClient(httpFactory.CreateClient("Api")).RedeemWebLinkAsync(code); }
    catch { /* API unreachable: fall through to the login page */ }
    if (string.IsNullOrEmpty(token)) return Results.Redirect("/login");

    context.Response.Cookies.Append("access_token", token, new CookieOptions
    {
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.MaxValue,
        Secure = context.Request.IsHttps,
        IsEssential = true
    });
    var safeNext = next is { Length: > 1 } && next[0] == '/' && next[1] != '/' && next[1] != '\\' ? next : "/";
    return Results.Redirect(safeNext);
});

// all the style sheets in one file; the address carries the hash of the content, so the browser may keep it for a year
app.MapGet("/site.css", (HttpContext context, SiteCss css) =>
{
    context.Response.Headers.CacheControl = context.Request.Query["v"] == css.Version ? "public, max-age=31536000, immutable" : "no-cache";
    return Results.Text(css.Content, "text/css; charset=utf-8");
});

app.MapLegalPages();     // /privacy and /delete-account: plain HTML, open to everybody

app.MapGet("/session/logout", (HttpContext context) =>
{
    // Same attributes as when the cookie was set, so every browser really drops it.
    context.Response.Cookies.Delete("access_token", new CookieOptions
    {
        Path = "/",
        HttpOnly = true,
        SameSite = SameSiteMode.Lax,
        Secure = context.Request.IsHttps,
        IsEssential = true
    });
    context.Response.Headers.CacheControl = "no-store";
    return Results.Redirect("/");
});

// ── HTTP pipeline ─────────────────────────────────────────────────────────────
// Runs behind Nginx, which is the sole TLS terminator (now over plain http,
// https once a domain + cert are added) — the app itself must never redirect
// to https, since Kestrel here never listens on a TLS port.
var forwardedOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
// Containers sit behind nginx (and jumbo.uz's front proxy) on private networks; trust them all.
forwardedOptions.KnownNetworks.Clear();
forwardedOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedOptions);

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();
if (app.Environment.IsDevelopment() && builder.Configuration["Media:VideosRoot"] is { Length: > 0 } devVideos)
{
    // on the server nginx serves /videos/ from the media folder; here the site does it, so a video can be tried
    Directory.CreateDirectory(devVideos);
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(devVideos),
        RequestPath = "/videos"
    });
}
app.MapStaticAssets();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(JumboWeb.Client._Imports).Assembly);

app.Run();
