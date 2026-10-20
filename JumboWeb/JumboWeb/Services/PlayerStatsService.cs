using Jumbo.ApiClient;
using Jumbo.Shared;

namespace JumboWeb.Services;

// What the strip under the top bar shows besides lives and LP: the book the learner is in, the level that follows it, the streak.
public class PlayerStatsService(LpApiClient apiClient)
{
    public int CurrentBook { get; private set; } = 1;

    public int Streak { get; private set; }

    // Streak freezes the learner has bought and not used yet.
    public int FreezeCount { get; private set; }

    public string Level => LanguageLevels.ForBook(CurrentBook);

    public async Task RefreshAsync()
    {
        try
        {
            CurrentBook = Math.Max(1, await apiClient.GetCurrentBookAsync());
            var daily = await apiClient.GetDailyProgressAsync();
            Streak = daily?.Streak ?? 0;
            FreezeCount = daily?.FreezeCount ?? 0;
        }
        catch
        {
            // decorative: a failed read keeps the last numbers
        }
    }
}
