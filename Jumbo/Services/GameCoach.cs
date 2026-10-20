using CommunityToolkit.Mvvm.ComponentModel;

namespace Jumbo.Services;

/// <summary>
/// Jumbo as the player's coach in a game: cheers a right answer, comforts after a wrong one (same phrases as the website),
/// then calms down again. Each game model owns one and binds <see cref="Mood"/> / <see cref="Text"/> to a TigerCoach.
/// </summary>
public partial class GameCoach : ObservableObject
{
    private int _generation;

    [ObservableProperty]
    private string _mood = "idle";

    [ObservableProperty]
    private string _text = string.Empty;

    [ObservableProperty]
    private bool _hasText;

    public void Cheer() => Say("cheer", $"Tiger_Cheer{Random.Shared.Next(1, 4)}");

    public void Oops() => Say("sad", $"Tiger_Oops{Random.Shared.Next(1, 4)}");

    /// <summary>Back to the quiet pose with no speech bubble.</summary>
    public void Reset()
    {
        _generation++;
        Mood = "idle";
        Text = string.Empty;
        HasText = false;
    }

    private void Say(string mood, string key)
    {
        int gen = ++_generation;
        Mood = mood;
        Text = LocalizationManager.Get(key);
        HasText = true;

#if DEBUG
        if (Environment.GetEnvironmentVariable("JUMBO_COACH_HOLD") == "1") return;   // screenshots: keep the speech bubble
#endif
        _ = Task.Delay(1800).ContinueWith(_ => MainThread.BeginInvokeOnMainThread(() =>
        {
            if (gen == _generation) Reset();
        }));
    }
}
