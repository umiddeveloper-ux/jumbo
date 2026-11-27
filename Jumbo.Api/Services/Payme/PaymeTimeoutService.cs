namespace Jumbo.Api.Services.Payme;

/// <summary>Every minute: the Payme transactions that were created more than 12 hours ago and never paid are cancelled (reason 4).</summary>
public class PaymeTimeoutService(IServiceScopeFactory scopes, ILogger<PaymeTimeoutService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    using var scope = scopes.CreateScope();
                    var cancelled = await scope.ServiceProvider.GetRequiredService<PaymeService>().ExpireAsync();
                    if (cancelled > 0) log.LogInformation("Payme: {Count} transactions cancelled by the 12 hour timeout", cancelled);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    log.LogError(ex, "Payme: the timeout job failed");
                }
            }
        }
        catch (OperationCanceledException) { /* the API is stopping */ }
    }
}
