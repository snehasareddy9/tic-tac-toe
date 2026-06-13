namespace Backend;

public record ScoreboardSnapshot(int X, int O, int Draws);

// Session-level scoreboard owned by the backend. Resets on server restart.
// Records each completed game exactly once (guarded by game id).
public class Scoreboard
{
    private readonly object _lock = new();
    private readonly HashSet<string> _recorded = new();
    private int _x, _o, _draws;

    public void Record(Game g)
    {
        lock (_lock)
        {
            if (g.Status == GameStatus.InProgress) return;
            if (!_recorded.Add(g.Id)) return;
            if (g.Winner == "X") _x++;
            else if (g.Winner == "O") _o++;
            else _draws++;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _x = 0; _o = 0; _draws = 0;
            _recorded.Clear();
        }
    }

    // Drop a single game id from the "already recorded" set so the same game
    // object can be counted again after Reset Game (board cleared, id preserved).
    public void Forget(string id)
    {
        lock (_lock) { _recorded.Remove(id); }
    }

    public ScoreboardSnapshot Snapshot()
    {
        lock (_lock) { return new ScoreboardSnapshot(_x, _o, _draws); }
    }
}
