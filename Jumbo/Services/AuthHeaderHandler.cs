namespace Jumbo.Services;

public class AuthHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var token = UserSession.AccessToken;
        if (!string.IsNullOrEmpty(token))
            request.Headers.Add("X-Access-Token", token);

        return base.SendAsync(request, cancellationToken);
    }
}
