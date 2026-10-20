#if DEBUG
using System.Diagnostics;

namespace Jumbo.Services;

/// <summary>Debug builds only: can add a delay (JUMBO_LATENCY, ms) and writes every request of the app and the time it took to the log (tag JUMBOHTTP), to see what a page loads.</summary>
public class TimingHandler : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // JUMBO_LATENCY=150 makes every request wait 150 ms first: the network of a phone, to feel what a child feels
        var sw = Stopwatch.StartNew();
        if (int.TryParse(Environment.GetEnvironmentVariable("JUMBO_LATENCY"), out var delay) && delay > 0) await Task.Delay(delay, cancellationToken);
        var response = await base.SendAsync(request, cancellationToken);
        Debug.WriteLine($"JUMBOHTTP {sw.ElapsedMilliseconds,5} ms {(int)response.StatusCode} {request.Method} {request.RequestUri?.PathAndQuery}");
        return response;
    }
}
#endif
