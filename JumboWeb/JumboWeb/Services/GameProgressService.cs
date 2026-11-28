using Jumbo.ApiClient;
using Jumbo.Shared;
using Jumbo.Shared.Dtos;
using Jumbo.Shared.Entities;

namespace JumboWeb.Services;

// The place a learner left a game at (see GameProgressController): the games save it after every answer and ask for it when they open. Everything here
// is best effort: a failure never reaches the game (a lost save only means the learner starts that game over). Only a signed-in learner has saved games;
// for a visitor, a teacher, a director or the admin nothing is sent.
public class GameProgressService(LpApiClient api, UserStateService user)
{
    public bool Enabled => user.CurrentUser is { Role: UserRole.Learner };

    private sealed class Slot
    {
        public SaveGameProgressRequest? Latest;      // the newest state that has not been sent yet
        public Task? Running;                        // the sender that is working now
    }

    private readonly object _lock = new();
    private readonly Dictionary<string, Slot> _slots = new();

    private static string KeyOf(string game, int subjectId) => $"{game}:{subjectId}";

    /// <summary>The saved game with its state, or null.</summary>
    public async Task<GameProgressDto?> LoadAsync(string game, int subjectId)
    {
        try { await user.EnsureLoadedAsync(); } catch { }
        if (!Enabled || subjectId <= 0) return null;
        try { return await api.GetGameProgressAsync(game, subjectId); }
        catch { return null; }
    }

    /// <summary>
    /// Saves the state after an answer. It does not wait: while one save is on its way the newest state waits and goes next (so a quick player never
    /// makes a queue and the last state always arrives). The state is the game's own object (Jumbo.Shared.GameStates).
    /// </summary>
    public void Save<T>(string game, int subjectId, T state, int done, int total)
    {
        if (!Enabled || subjectId <= 0) return;
        var request = new SaveGameProgressRequest { Game = game, SubjectId = subjectId, State = GameStates.Write(state), Done = done, Total = total };
        var key = KeyOf(game, subjectId);
        lock (_lock)
        {
            if (!_slots.TryGetValue(key, out var slot)) _slots[key] = slot = new Slot();
            slot.Latest = request;
            if (slot.Running is not null) return;
            slot.Running = Task.Run(() => PumpAsync(key, slot));
        }
    }

    private async Task PumpAsync(string key, Slot slot)
    {
        while (true)
        {
            SaveGameProgressRequest? next;
            lock (_lock)
            {
                next = slot.Latest;
                slot.Latest = null;
                if (next is null) { slot.Running = null; return; }
            }
            try { await api.SaveGameProgressAsync(next.Game, next.SubjectId, next.State, next.Done, next.Total); }
            catch { /* a lost save: the learner will start that game over, nothing else */ }
        }
    }

    /// <summary>The saved game is not needed any more (the game was lost, or the learner starts over): what waits to be sent is dropped.</summary>
    public async Task DeleteAsync(string game, int subjectId)
    {
        if (!Enabled || subjectId <= 0) return;
        Task? running;
        lock (_lock)
        {
            if (_slots.TryGetValue(KeyOf(game, subjectId), out var slot)) { slot.Latest = null; running = slot.Running; }
            else running = null;
        }
        // a save that is on its way must arrive before the delete, else it would bring the saved game back
        if (running is not null) { try { await running; } catch { } }
        try { await api.DeleteGameProgressAsync(game, subjectId); }
        catch { }
    }

    /// <summary>The learner's unfinished games for the badges (empty for anybody else).</summary>
    public async Task<List<GameProgressSummaryDto>> UnfinishedAsync()
    {
        try { await user.EnsureLoadedAsync(); } catch { }
        if (!Enabled) return [];
        try { return await api.GetUnfinishedGamesAsync(); }
        catch { return []; }
    }
}
